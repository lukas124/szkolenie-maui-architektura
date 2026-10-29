using System.Net;
using System.Text;
using System.Text.Json;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Outbox;
using FieldService.Infrastructure.Visits;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Visits;

// Atrapa sieci: zapamiętuje żądanie (z ciałem) i zwraca zadaną odpowiedź albo rzuca wyjątek
internal sealed class CapturingHandler : HttpMessageHandler
{
    public Func<HttpResponseMessage> Respond { get; set; } = () => new HttpResponseMessage(HttpStatusCode.OK);
    public Exception? Throw { get; set; }
    public bool WaitForever { get; set; }

    public HttpRequestMessage? Request { get; private set; }
    public string? Body { get; private set; }
    public string? ContentType { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        if (request.Content is not null)
        {
            Body = await request.Content.ReadAsStringAsync(cancellationToken);
            ContentType = request.Content.Headers.ContentType?.MediaType;
        }

        if (WaitForever)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        if (Throw is not null)
            throw Throw;
        return Respond();
    }
}

// HttpVisitsGateway jako IOutboxSender: wiadomość outbox → żądanie HTTP i odpowiedź → Result<int>
[TestFixture]
public class HttpVisitsGatewaySendTests
{
    private const string Token = "token-technika-123";
    private const string VisitGuid = "3f2b8c1e-5d4a-4e7b-9c61-0a1b2c3d4e5f";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private CapturingHandler _handler = null!;
    private HttpClient _http = null!;
    private HttpVisitsGateway _gateway = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new CapturingHandler { Respond = () => VersionResponse(8) };
        _http = new HttpClient(_handler) { BaseAddress = new Uri("https://api.test/") };
        _gateway = new HttpVisitsGateway(_http, new AccessTokenStore { AccessToken = Token });
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    private static OutboxMessage Message(string type, string payload) => new()
    {
        Id = "a1b2c3d4-0000-4000-8000-000000000001",
        Type = type,
        VisitId = VisitGuid,
        Payload = payload,
        CreatedAt = "2026-03-10T09:00:00.0000000+00:00",
        Attempts = 0,
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) };

    private static HttpResponseMessage VersionResponse(int version) =>
        Json(HttpStatusCode.OK, $$"""{"id":"{{VisitGuid}}","version":{{version}}}""");

    private static IEnumerable<TestCaseData> Requests()
    {
        yield return new TestCaseData("VisitStarted", """{"startedAt":"2026-03-10T09:00:00+00:00"}""",
            "POST", $"/api/visits/{VisitGuid}/start");
        yield return new TestCaseData("ActivityRecorded",
            """{"code":"DIAG","description":"Diagnostyka","recordedAt":"2026-03-10T09:05:00+00:00"}""",
            "POST", $"/api/visits/{VisitGuid}/activities");
        yield return new TestCaseData("PartUsed", """{"partId":"P-FILTR-01","quantity":2}""",
            "POST", $"/api/visits/{VisitGuid}/parts");
        yield return new TestCaseData("PartRemoved", """{"partId":"P-FILTR-01"}""",
            "DELETE", $"/api/visits/{VisitGuid}/parts/P-FILTR-01");
        yield return new TestCaseData("CustomerSigned", """{"signerName":"Anna Nowak","signedAt":"2026-03-10T09:30:00+00:00"}""",
            "PUT", $"/api/visits/{VisitGuid}/signature");
        yield return new TestCaseData("VisitCompleted",
            """{"outcome":"Fixed","reason":null,"completedAt":"2026-03-10T09:40:00+00:00"}""",
            "POST", $"/api/visits/{VisitGuid}/complete");
    }

    private static IEnumerable<TestCaseData> RequestsWithBody() =>
        Requests().Where(c => (string)c.Arguments[0]! != "PartRemoved");

    [TestCaseSource(nameof(Requests))]
    public async Task SendAsync_MessageType_UsesMethodAndPathFromTable(string type, string payload, string method, string path)
    {
        await _gateway.SendAsync(Message(type, payload), 7, Ct);

        Assert.That(_handler.Request, Is.Not.Null);
        Assert.That(_handler.Request!.Method.Method, Is.EqualTo(method));
        Assert.That(_handler.Request.RequestUri!.Host, Is.EqualTo("api.test"));
        Assert.That(_handler.Request.RequestUri.AbsolutePath, Is.EqualTo(path));
    }

    [TestCaseSource(nameof(Requests))]
    public async Task SendAsync_AnyType_AddsAuthorizationIdempotencyAndVersionHeaders(string type, string payload, string method, string path)
    {
        await _gateway.SendAsync(Message(type, payload), 7, Ct);

        var request = _handler.Request!;
        Assert.That(request.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
        Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo(Token));
        Assert.That(request.Headers.GetValues("Idempotency-Key").Single(), Is.EqualTo("a1b2c3d4-0000-4000-8000-000000000001"));
        Assert.That(request.Headers.GetValues("If-Match").Single(), Is.EqualTo("\"7\""));
        Assert.That(request.Headers.GetValues("X-If-Match").Single(), Is.EqualTo("\"7\""));
    }

    [Test]
    public async Task SendAsync_DifferentVersion_SendsItInIfMatchAndXIfMatch()
    {
        await _gateway.SendAsync(Message("PartUsed", """{"partId":"P-FILTR-01","quantity":1}"""), 42, Ct);

        Assert.That(_handler.Request!.Headers.GetValues("If-Match").Single(), Is.EqualTo("\"42\""));
        Assert.That(_handler.Request.Headers.GetValues("X-If-Match").Single(), Is.EqualTo("\"42\""));
    }

    [Test]
    public async Task SendAsync_SubsequentMessages_IdempotencyKeyIsEachMessageId()
    {
        var message = Message("PartUsed", """{"partId":"P-FILTR-01","quantity":1}""");
        message.Id = "b7e6d5c4-1111-4222-8333-444455556666";

        await _gateway.SendAsync(message, 1, Ct);

        Assert.That(_handler.Request!.Headers.GetValues("Idempotency-Key").Single(), Is.EqualTo(message.Id));
    }

    [TestCaseSource(nameof(RequestsWithBody))]
    public async Task SendAsync_TypeWithBody_SendsPayloadAsJson(string type, string payload, string method, string path)
    {
        await _gateway.SendAsync(Message(type, payload), 7, Ct);

        Assert.That(_handler.ContentType, Is.EqualTo("application/json"));
        using var expected = JsonDocument.Parse(payload);
        using var actual = JsonDocument.Parse(_handler.Body!);
        Assert.That(JsonElement.DeepEquals(expected.RootElement, actual.RootElement), Is.True,
            $"ciało: {_handler.Body}");
    }

    [Test]
    public async Task SendAsync_PartRemoved_SendsDeleteWithoutBody()
    {
        await _gateway.SendAsync(Message("PartRemoved", """{"partId":"P-FILTR-01"}"""), 7, Ct);

        Assert.That(_handler.Body, Is.Null.Or.Empty);
    }

    [Test]
    public async Task SendAsync_Response200_ReturnsVersionFromBody()
    {
        _handler.Respond = () => VersionResponse(13);

        var result = await _gateway.SendAsync(Message("VisitStarted", """{"startedAt":"2026-03-10T09:00:00+00:00"}"""), 12, Ct);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(13));
    }

    [Test]
    public async Task SendAsync_Response409WithVersionConflictCode_ReturnsVersionConflict()
    {
        _handler.Respond = () => Json(HttpStatusCode.Conflict,
            """{"status":409,"code":"Visit.VersionConflict","currentVersion":7,"currentStatus":"Cancelled"}""",
            "application/problem+json");

        var result = await _gateway.SendAsync(Message("VisitCompleted",
            """{"outcome":"Fixed","reason":null,"completedAt":"2026-03-10T09:40:00+00:00"}"""), 5, Ct);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(VisitErrors.VersionConflict));
    }

    [Test]
    public async Task SendAsync_Response409WithOtherCode_ReturnsUnexpected()
    {
        _handler.Respond = () => Json(HttpStatusCode.Conflict,
            """{"status":409,"code":"Visit.InvalidTransition"}""", "application/problem+json");

        var result = await _gateway.SendAsync(Message("VisitStarted", """{"startedAt":"2026-03-10T09:00:00+00:00"}"""), 5, Ct);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error!.Code, Is.EqualTo("Visits.Unexpected"));
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Unexpected));
    }

    [Test]
    public async Task SendAsync_Response401_ReturnsUnauthorized()
    {
        _handler.Respond = () => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var result = await _gateway.SendAsync(Message("VisitStarted", """{"startedAt":"2026-03-10T09:00:00+00:00"}"""), 5, Ct);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error!.Code, Is.EqualTo("Visits.Unauthorized"));
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Unauthorized));
    }

    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.UnprocessableEntity)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_OtherStatus_ReturnsUnexpected(HttpStatusCode status)
    {
        _handler.Respond = () => Json(status, """{"status":0,"code":"Something.Else"}""", "application/problem+json");

        var result = await _gateway.SendAsync(Message("VisitStarted", """{"startedAt":"2026-03-10T09:00:00+00:00"}"""), 5, Ct);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error!.Code, Is.EqualTo("Visits.Unexpected"));
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Unexpected));
    }

    [Test]
    public async Task SendAsync_HttpRequestException_ReturnsNetwork()
    {
        _handler.Throw = new HttpRequestException("Brak połączenia");

        var result = await _gateway.SendAsync(Message("VisitStarted", """{"startedAt":"2026-03-10T09:00:00+00:00"}"""), 5, Ct);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error!.Code, Is.EqualTo("Visits.Network"));
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Network));
    }

    [Test]
    public async Task SendAsync_Timeout_ReturnsNetwork()
    {
        _handler.WaitForever = true;
        _http.Timeout = TimeSpan.FromMilliseconds(100);

        var result = await _gateway.SendAsync(Message("VisitStarted", """{"startedAt":"2026-03-10T09:00:00+00:00"}"""), 5, Ct);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error!.Code, Is.EqualTo("Visits.Network"));
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Network));
    }
}
