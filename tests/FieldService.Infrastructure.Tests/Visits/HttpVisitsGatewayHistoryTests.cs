using System.Net;
using System.Text;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Visits;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Visits;

public class HttpVisitsGatewayHistoryTests
{
    private const string Token = "token-123";
    private static readonly Guid FirstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Odpowiedź serwera jak w kontrakcie: wizyta zakończona i wizyta anulowana (outcome null)
    private const string PageJson = """
        { "items": [
            { "id": "11111111-1111-1111-1111-111111111111", "workOrderNumber": "ZL-2610-1001", "status": "Completed",
              "plannedStart": "2026-10-28T08:00:00+00:00", "completedAt": "2026-10-28T09:10:00+00:00",
              "customerName": "Jan Klient", "city": "Piaseczno", "deviceName": "Pompa ciepła 8 kW",
              "outcome": "NeedsFollowUp", "thumbnailUrl": "https://api.test/api/images/abc?size=thumb" },
            { "id": "22222222-2222-2222-2222-222222222222", "workOrderNumber": "ZL-2610-1002", "status": "Cancelled",
              "plannedStart": "2026-10-27T12:00:00+00:00", "completedAt": null,
              "customerName": "Anna Nowak", "city": "Warszawa", "deviceName": "Kocioł gazowy",
              "outcome": null, "thumbnailUrl": "https://api.test/api/images/def?size=thumb" } ],
          "page": 2, "pageSize": 30, "totalCount": 65 }
        """;

    // Własny handler HTTP: zapamiętuje żądanie i zwraca przygotowaną odpowiedź albo rzuca wyjątek
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpVisitsGateway CreateGateway(StubHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        return new HttpVisitsGateway(http, new AccessTokenStore { AccessToken = Token });
    }

    [Test]
    public async Task GetHistoryAsync_AnyPage_SendsGetWithPageAndPageSizeInQuery()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, PageJson));
        var gateway = CreateGateway(handler);

        await gateway.GetHistoryAsync(2, 30, CancellationToken.None);

        Assert.That(handler.LastRequest, Is.Not.Null);
        Assert.That(handler.LastRequest!.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(handler.LastRequest.RequestUri!.AbsolutePath, Is.EqualTo("/api/visits/history"));
        Assert.That(handler.LastRequest.RequestUri.Query, Is.EqualTo("?page=2&pageSize=30"));
    }

    [Test]
    public async Task GetHistoryAsync_TokenStored_SendsBearerAuthorization()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, PageJson));
        var gateway = CreateGateway(handler);

        await gateway.GetHistoryAsync(1, 30, CancellationToken.None);

        var authorization = handler.LastRequest!.Headers.Authorization;
        Assert.That(authorization, Is.Not.Null);
        Assert.That(authorization!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(authorization.Parameter, Is.EqualTo(Token));
    }

    [Test]
    public async Task GetHistoryAsync_Ok_ReturnsPageAndTotalCount()
    {
        var gateway = CreateGateway(new StubHandler(_ => Json(HttpStatusCode.OK, PageJson)));

        var result = await gateway.GetHistoryAsync(2, 30, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Page, Is.EqualTo(2));
        Assert.That(result.Value.TotalCount, Is.EqualTo(65));
        Assert.That(result.Value.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetHistoryAsync_CompletedEntry_MapsAllFields()
    {
        var gateway = CreateGateway(new StubHandler(_ => Json(HttpStatusCode.OK, PageJson)));

        var result = await gateway.GetHistoryAsync(2, 30, CancellationToken.None);

        var entry = result.Value.Items[0];
        Assert.That(entry.Id, Is.EqualTo(FirstId));
        Assert.That(entry.WorkOrderNumber, Is.EqualTo("ZL-2610-1001"));
        Assert.That(entry.Status, Is.EqualTo(VisitStatus.Completed));
        Assert.That(entry.PlannedStart, Is.EqualTo(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero)));
        Assert.That(entry.CompletedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 28, 9, 10, 0, TimeSpan.Zero)));
        Assert.That(entry.CustomerName, Is.EqualTo("Jan Klient"));
        Assert.That(entry.City, Is.EqualTo("Piaseczno"));
        Assert.That(entry.DeviceName, Is.EqualTo("Pompa ciepła 8 kW"));
        Assert.That(entry.Outcome, Is.EqualTo(VisitOutcomeType.NeedsFollowUp));
        Assert.That(entry.ThumbnailUrl, Is.EqualTo("https://api.test/api/images/abc?size=thumb"));
    }

    [Test]
    public async Task GetHistoryAsync_ThumbnailWithSizeParameter_PhotoUrlWithoutParameter()
    {
        var gateway = CreateGateway(new StubHandler(_ => Json(HttpStatusCode.OK, PageJson)));

        var result = await gateway.GetHistoryAsync(2, 30, CancellationToken.None);

        Assert.That(result.Value.Items[0].PhotoUrl, Is.EqualTo("https://api.test/api/images/abc"));
        Assert.That(result.Value.Items[1].PhotoUrl, Is.EqualTo("https://api.test/api/images/def"));
    }

    [Test]
    public async Task GetHistoryAsync_CancelledEntryWithNullOutcome_MapsStatusAndNullOutcome()
    {
        var gateway = CreateGateway(new StubHandler(_ => Json(HttpStatusCode.OK, PageJson)));

        var result = await gateway.GetHistoryAsync(2, 30, CancellationToken.None);

        var entry = result.Value.Items[1];
        Assert.That(entry.Id, Is.EqualTo(SecondId));
        Assert.That(entry.Status, Is.EqualTo(VisitStatus.Cancelled));
        Assert.That(entry.Outcome, Is.Null);
        Assert.That(entry.CompletedAt, Is.Null);
    }

    [Test]
    public async Task GetHistoryAsync_Unauthorized_ReturnsUnauthorizedError()
    {
        var gateway = CreateGateway(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var result = await gateway.GetHistoryAsync(1, 30, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(new Error("Visits.Unauthorized", ErrorType.Unauthorized)));
    }

    [Test]
    public async Task GetHistoryAsync_NetworkException_ReturnsNetworkError()
    {
        var gateway = CreateGateway(new StubHandler(_ => throw new HttpRequestException("brak sieci")));

        var result = await gateway.GetHistoryAsync(1, 30, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(new Error("Visits.Network", ErrorType.Network)));
    }

    [Test]
    public async Task GetHistoryAsync_ServerError_ReturnsUnexpectedError()
    {
        var gateway = CreateGateway(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await gateway.GetHistoryAsync(1, 30, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(new Error("Visits.Unexpected", ErrorType.Unexpected)));
    }

    [Test]
    public async Task GetHistoryAsync_BodyNotJson_ReturnsUnexpectedError()
    {
        var gateway = CreateGateway(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>błąd</html>", Encoding.UTF8, "text/html")
        }));

        var result = await gateway.GetHistoryAsync(1, 30, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(new Error("Visits.Unexpected", ErrorType.Unexpected)));
    }
}
