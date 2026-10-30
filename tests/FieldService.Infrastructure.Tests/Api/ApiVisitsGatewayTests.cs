using FieldService.Infrastructure.Visits;
using System.Net;
using FieldService.Application.Visits;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Api;
using FieldService.Infrastructure.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using VisitOutcomeType = FieldService.Domain.Visits.VisitOutcomeType;
using VisitStatus = FieldService.Domain.Visits.VisitStatus;

namespace FieldService.Infrastructure.Tests.Api;

[TestFixture]
public class ApiVisitsGatewayTests
{
    private static readonly Guid VisitGuid = Guid.Parse("3a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private static readonly Guid OtherVisitGuid = Guid.Parse("7d6c5b4a-3f2e-4d1c-9b8a-7f6e5d4c3b2a");
    private static readonly Guid WorkOrderGuid = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private const string MessageId = "9f8e7d6c-5b4a-4392-8170-6f5e4d3c2b1a";

    private StubHttpHandler _stub = null!;
    private ApiVisitsGateway _gateway = null!;

    [TearDown]

    public void TearDown() => _stub.Dispose();


    [SetUp]
    public void SetUp()
    {
        _stub = new StubHttpHandler();
        var client = new FieldServiceApiClient(new HttpClient(_stub) { BaseAddress = new Uri("https://api.test/") });
        _gateway = new ApiVisitsGateway(client, new ApiExecutor(NullLogger<ApiExecutor>.Instance));
    }

    // Pełna odpowiedź VisitDetailsDto (wszystkie pola wymagane w specyfikacji)
    private static string VisitDetailsJson(int version, string status = "InProgress") =>
        $"{{\"id\":\"{VisitGuid}\",\"workOrderId\":\"{WorkOrderGuid}\",\"workOrderNumber\":\"ZL-2610-1001\"," +
        $"\"status\":\"{status}\",\"plannedStart\":\"2026-10-30T08:00:00+00:00\",\"plannedEnd\":\"2026-10-30T10:00:00+00:00\"," +
        "\"startedAt\":\"2026-10-30T08:15:00+00:00\",\"completedAt\":null," +
        "\"customer\":{\"name\":\"Jan Klient\",\"phone\":\"+48 600 100 200\",\"street\":\"ul. Leśna 5\",\"city\":\"Piaseczno\"}," +
        "\"deviceName\":\"Pompa ciepła 8 kW\",\"faultDescription\":\"Nie grzeje wody\"," +
        "\"slaDueAt\":\"2026-10-30T16:00:00+00:00\",\"technicianNames\":[\"Jan Kowalski\"]," +
        "\"technicianEmails\":[\"jan.kowalski@firma.pl\"],\"activities\":[],\"parts\":[],\"scopeVersion\":1," +
        "\"thumbnailUrl\":\"https://api.test/api/images/pompa?size=thumb\",\"imageUrl\":\"https://api.test/api/images/pompa\"," +
        $"\"version\":{version}}}";

    private static string VisitSummaryJson(Guid id, string number, string status) =>
        $"{{\"id\":\"{id}\",\"workOrderId\":\"{WorkOrderGuid}\",\"workOrderNumber\":\"{number}\",\"status\":\"{status}\"," +
        "\"plannedStart\":\"2026-10-30T08:00:00+00:00\",\"plannedEnd\":\"2026-10-30T10:00:00+00:00\"," +
        "\"customerName\":\"Jan Klient\",\"address\":\"ul. Leśna 5, Piaseczno\",\"deviceName\":\"Pompa ciepła 8 kW\"," +
        "\"faultDescription\":\"Nie grzeje wody\",\"isBrigade\":false," +
        "\"thumbnailUrl\":\"https://api.test/api/images/pompa?size=thumb\",\"version\":3}";

    private static OutboxMessage Message(string type, string payload) => new()
    {
        Id = MessageId,
        Type = type,
        VisitId = VisitGuid.ToString("D"),
        Payload = payload,
        CreatedAt = "2026-10-30T08:15:00.0000000+00:00",
        Attempts = 0,
    };

    // ---------- Lista na dziś ----------

    // GET api/visits/today → po jednym VisitSummary na element
    [Test]
    public async Task GetToday_Success_MapsAllVisits()
    {
        _stub.Respond(HttpStatusCode.OK,
            $"[{VisitSummaryJson(VisitGuid, "ZL-2610-1001", "InProgress")},{VisitSummaryJson(OtherVisitGuid, "ZL-2610-1002", "Planned")}]");

        var result = await _gateway.GetTodayAsync(CancellationToken.None);

        Assert.That(_stub.LastRequest.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(_stub.LastRequest.Path, Does.EndWith("/api/visits/today"));
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Has.Count.EqualTo(2));
    }

    // 401 → Api.Unauthorized
    [Test]
    public async Task GetToday_401_ReturnsUnauthorized()
    {
        _stub.Respond(HttpStatusCode.Unauthorized);

        var result = await _gateway.GetTodayAsync(CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Api.Unauthorized", ErrorType.Unauthorized)));
    }

    // Brak sieci → Network
    [Test]
    public async Task GetToday_HttpRequestException_ReturnsNetwork()
    {
        _stub.Throw(new HttpRequestException("brak sieci"));

        var result = await _gateway.GetTodayAsync(CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // Odpowiedź nie-JSON przy 200 (portal logowania Wi-Fi) → InvalidResponse
    [Test]
    public async Task GetToday_NonJsonResponse_ReturnsInvalidResponse()
    {
        _stub.Respond(HttpStatusCode.OK, "<html>Zaloguj się do sieci</html>", "text/html");

        var result = await _gateway.GetTodayAsync(CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.InvalidResponse));
    }

    // ---------- Historia ----------

    private const string HistoryJson =
        "{\"items\":[" +
        "{\"id\":\"3a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d\",\"workOrderNumber\":\"ZL-2610-1001\",\"status\":\"Completed\"," +
        "\"plannedStart\":\"2026-10-28T08:00:00+00:00\",\"completedAt\":\"2026-10-28T09:10:00+00:00\"," +
        "\"customerName\":\"Jan Klient\",\"city\":\"Piaseczno\",\"deviceName\":\"Pompa ciepła 8 kW\"," +
        "\"outcome\":\"Fixed\",\"thumbnailUrl\":\"https://api.test/api/images/abc?size=thumb\"}," +
        "{\"id\":\"7d6c5b4a-3f2e-4d1c-9b8a-7f6e5d4c3b2a\",\"workOrderNumber\":\"ZL-2610-0990\",\"status\":\"Cancelled\"," +
        "\"plannedStart\":\"2026-10-27T12:00:00+00:00\",\"completedAt\":null," +
        "\"customerName\":\"Ewa Nowak\",\"city\":\"Warszawa\",\"deviceName\":\"Kocioł gazowy\"," +
        "\"thumbnailUrl\":\"https://api.test/api/images/def?size=thumb\"}]," +
        "\"page\":2,\"pageSize\":30,\"totalCount\":65}";

    // GET api/visits/history?page=2&pageSize=30
    [Test]
    public async Task GetHistory_SendsPageAndPageSize()
    {
        _stub.Respond(HttpStatusCode.OK, HistoryJson);

        await _gateway.GetHistoryAsync(2, 30, CancellationToken.None);

        var request = _stub.LastRequest;
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(request.Path, Does.EndWith("/api/visits/history"));
        Assert.That(request.Query, Does.Contain("page=2"));
        Assert.That(request.Query, Does.Contain("pageSize=30"));
    }

    // Strona, łączna liczba i wpisy; PhotoUrl = thumbnailUrl bez ?size=thumb
    [Test]
    public async Task GetHistory_Success_MapsPage()
    {
        _stub.Respond(HttpStatusCode.OK, HistoryJson);

        var result = await _gateway.GetHistoryAsync(2, 30, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Page, Is.EqualTo(2));
        Assert.That(result.Value.TotalCount, Is.EqualTo(65));
        Assert.That(result.Value.Items, Is.EqualTo(new[]
        {
            new VisitHistoryEntry(VisitGuid, "ZL-2610-1001", VisitStatus.Completed,
                new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 28, 9, 10, 0, TimeSpan.Zero),
                "Jan Klient", "Piaseczno", "Pompa ciepła 8 kW", VisitOutcomeType.Fixed,
                "https://api.test/api/images/abc?size=thumb", "https://api.test/api/images/abc"),
            new VisitHistoryEntry(OtherVisitGuid, "ZL-2610-0990", VisitStatus.Cancelled,
                new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero),
                null,
                "Ewa Nowak", "Warszawa", "Kocioł gazowy", null,
                "https://api.test/api/images/def?size=thumb", "https://api.test/api/images/def"),
        }));
    }

    // 401 → Api.Unauthorized
    [Test]
    public async Task GetHistory_401_ReturnsUnauthorized()
    {
        _stub.Respond(HttpStatusCode.Unauthorized);

        var result = await _gateway.GetHistoryAsync(1, 30, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Api.Unauthorized", ErrorType.Unauthorized)));
    }

    // Brak sieci → Network
    [Test]
    public async Task GetHistory_HttpRequestException_ReturnsNetwork()
    {
        _stub.Throw(new HttpRequestException("brak sieci"));

        var result = await _gateway.GetHistoryAsync(1, 30, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // 500 → Api.Unexpected
    [Test]
    public async Task GetHistory_500_ReturnsUnexpected()
    {
        _stub.Respond(HttpStatusCode.InternalServerError, "{\"status\":500}");

        var result = await _gateway.GetHistoryAsync(1, 30, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Api.Unexpected", ErrorType.Unexpected)));
    }

    // ---------- Szczegóły ----------

    // GET api/visits/{id} → RemoteVisit (migawka i wersja z serwera)
    [Test]
    public async Task GetVisit_Success_MapsSnapshotAndVersion()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(5));

        var result = await _gateway.GetVisitAsync(new VisitId(VisitGuid), CancellationToken.None);

        Assert.That(_stub.LastRequest.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(_stub.LastRequest.Path, Does.EndWith($"/api/visits/{VisitGuid}"));
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Visit.Version, Is.EqualTo(5));
        Assert.That(result.Value.Snapshot.WorkOrderNumber, Is.EqualTo("ZL-2610-1001"));
        Assert.That(result.Value.Snapshot.DeviceName, Is.EqualTo("Pompa ciepła 8 kW"));
        Assert.That(result.Value.Snapshot.FaultDescription, Is.EqualTo("Nie grzeje wody"));
        Assert.That(result.Value.Snapshot.Customer.Name, Is.EqualTo("Jan Klient"));
        Assert.That(result.Value.Snapshot.Customer.Phone, Is.EqualTo("+48 600 100 200"));
    }

    // 404 → VisitErrors.NotFound (nie ogólne Api.NotFound)
    [Test]
    public async Task GetVisit_404_ReturnsVisitNotFound()
    {
        _stub.Respond(HttpStatusCode.NotFound, "{\"status\":404,\"title\":\"Not Found\"}");

        var result = await _gateway.GetVisitAsync(new VisitId(VisitGuid), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotFound));
    }

    // ---------- Outbox: SendAsync ----------

    // Metoda i ścieżka dla każdego typu wiadomości (tabela z kontraktu 11)
    [TestCase("VisitStarted", "{\"startedAt\":\"2026-10-30T08:15:00+00:00\"}", "POST", "/start")]
    [TestCase("ActivityRecorded", "{\"code\":\"DIAG\",\"description\":\"Diagnostyka\",\"recordedAt\":\"2026-10-30T08:30:00+00:00\"}", "POST", "/activities")]
    [TestCase("PartUsed", "{\"partId\":\"FILTR-SIATKOWY-22\",\"quantity\":2}", "POST", "/parts")]
    [TestCase("PartRemoved", "{\"partId\":\"FILTR-SIATKOWY-22\"}", "DELETE", "/parts/FILTR-SIATKOWY-22")]
    [TestCase("CustomerSigned", "{\"signerName\":\"Anna Wiśniewska\",\"signedAt\":\"2026-10-30T09:40:00+00:00\"}", "PUT", "/signature")]
    [TestCase("VisitCompleted", "{\"outcome\":\"Fixed\",\"reason\":null,\"completedAt\":\"2026-10-30T09:45:00+00:00\"}", "POST", "/complete")]
    public async Task Send_EachType_UsesMethodAndPath(string type, string payload, string method, string pathSuffix)
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        await _gateway.SendAsync(Message(type, payload), 3, CancellationToken.None);

        Assert.That(_stub.Requests, Has.Count.EqualTo(1));
        Assert.That(_stub.LastRequest.Method, Is.EqualTo(new HttpMethod(method)));
        Assert.That(_stub.LastRequest.Path, Does.EndWith($"/api/visits/{VisitGuid}{pathSuffix}"));
    }

    // Nagłówki wersji (w cudzysłowach) i klucz idempotencji dla każdego typu
    [TestCase("VisitStarted", "{\"startedAt\":\"2026-10-30T08:15:00+00:00\"}")]
    [TestCase("PartRemoved", "{\"partId\":\"FILTR-SIATKOWY-22\"}")]
    [TestCase("CustomerSigned", "{\"signerName\":\"Anna Wiśniewska\",\"signedAt\":\"2026-10-30T09:40:00+00:00\"}")]
    public async Task Send_SetsVersionAndIdempotencyHeaders(string type, string payload)
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        await _gateway.SendAsync(Message(type, payload), 3, CancellationToken.None);

        var request = _stub.LastRequest;
        Assert.That(request.Header("If-Match"), Is.EqualTo("\"3\""));
        Assert.That(request.Header("X-If-Match"), Is.EqualTo("\"3\""));
        Assert.That(request.Header("Idempotency-Key"), Is.EqualTo(MessageId));
    }

    // Ciało VisitStarted: startedAt
    [Test]
    public async Task Send_VisitStarted_SendsStartedAt()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        await _gateway.SendAsync(Message("VisitStarted", "{\"startedAt\":\"2026-10-30T08:15:00+00:00\"}"), 3, CancellationToken.None);

        var body = _stub.LastRequest.Json();
        Assert.That(body.GetProperty("startedAt").GetDateTimeOffset(),
            Is.EqualTo(new DateTimeOffset(2026, 10, 30, 8, 15, 0, TimeSpan.Zero)));
    }

    // Ciało ActivityRecorded: code, description, recordedAt
    [Test]
    public async Task Send_ActivityRecorded_SendsFields()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        await _gateway.SendAsync(Message("ActivityRecorded",
            "{\"code\":\"DIAG\",\"description\":\"Diagnostyka układu\",\"recordedAt\":\"2026-10-30T08:30:00+00:00\"}"),
            3, CancellationToken.None);

        var body = _stub.LastRequest.Json();
        Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("DIAG"));
        Assert.That(body.GetProperty("description").GetString(), Is.EqualTo("Diagnostyka układu"));
        Assert.That(body.GetProperty("recordedAt").GetDateTimeOffset(),
            Is.EqualTo(new DateTimeOffset(2026, 10, 30, 8, 30, 0, TimeSpan.Zero)));
    }

    // Ciało PartUsed: partId, quantity (liczba)
    [Test]
    public async Task Send_PartUsed_SendsFields()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        await _gateway.SendAsync(Message("PartUsed", "{\"partId\":\"FILTR-SIATKOWY-22\",\"quantity\":2}"), 3, CancellationToken.None);

        var body = _stub.LastRequest.Json();
        Assert.That(body.GetProperty("partId").GetString(), Is.EqualTo("FILTR-SIATKOWY-22"));
        Assert.That(body.GetProperty("quantity").GetInt32(), Is.EqualTo(2));
    }

    // PartRemoved: DELETE bez ciała
    [Test]
    public async Task Send_PartRemoved_SendsNoBody()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        await _gateway.SendAsync(Message("PartRemoved", "{\"partId\":\"FILTR-SIATKOWY-22\"}"), 3, CancellationToken.None);

        Assert.That(_stub.LastRequest.Body, Is.Null.Or.Empty);
    }

    // Ciało CustomerSigned: signerName, signedAt
    [Test]
    public async Task Send_CustomerSigned_SendsFields()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        await _gateway.SendAsync(Message("CustomerSigned",
            "{\"signerName\":\"Anna Wiśniewska\",\"signedAt\":\"2026-10-30T09:40:00+00:00\"}"), 3, CancellationToken.None);

        var body = _stub.LastRequest.Json();
        Assert.That(body.GetProperty("signerName").GetString(), Is.EqualTo("Anna Wiśniewska"));
        Assert.That(body.GetProperty("signedAt").GetDateTimeOffset(),
            Is.EqualTo(new DateTimeOffset(2026, 10, 30, 9, 40, 0, TimeSpan.Zero)));
    }

    // Ciało VisitCompleted: outcome (tekst), reason, completedAt
    [Test]
    public async Task Send_VisitCompleted_SendsFields()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4, "Completed"));

        await _gateway.SendAsync(Message("VisitCompleted",
            "{\"outcome\":\"NeedsFollowUp\",\"reason\":\"Brak części\",\"completedAt\":\"2026-10-30T09:45:00+00:00\"}"),
            3, CancellationToken.None);

        var body = _stub.LastRequest.Json();
        Assert.That(body.GetProperty("outcome").GetString(), Is.EqualTo("NeedsFollowUp"));
        Assert.That(body.GetProperty("reason").GetString(), Is.EqualTo("Brak części"));
        Assert.That(body.GetProperty("completedAt").GetDateTimeOffset(),
            Is.EqualTo(new DateTimeOffset(2026, 10, 30, 9, 45, 0, TimeSpan.Zero)));
    }

    // 200 → nowa wersja wizyty z odpowiedzi
    [Test]
    public async Task Send_200_ReturnsVersionFromResponse()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        var result = await _gateway.SendAsync(Message("VisitStarted", "{\"startedAt\":\"2026-10-30T08:15:00+00:00\"}"), 3,
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(4));
    }

    // 409 Visit.VersionConflict → błąd równy VisitErrors.VersionConflict
    [Test]
    [Ignore("Ćwiczenie 13: mapowanie 409 w ApiErrorMapper")]
    public async Task Send_409VersionConflict_ReturnsVersionConflict()
    {
        _stub.Respond(HttpStatusCode.Conflict,
            "{\"status\":409,\"code\":\"Visit.VersionConflict\",\"currentVersion\":7,\"currentStatus\":\"Cancelled\"}",
            "application/problem+json");

        var result = await _gateway.SendAsync(Message("VisitStarted", "{\"startedAt\":\"2026-10-30T08:15:00+00:00\"}"), 3,
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(VisitErrors.VersionConflict));
    }

    // 401 → Api.Unauthorized (OutboxProcessor kończy przebieg)
    [Test]
    public async Task Send_401_ReturnsUnauthorized()
    {
        _stub.Respond(HttpStatusCode.Unauthorized);

        var result = await _gateway.SendAsync(Message("PartUsed", "{\"partId\":\"FILTR-SIATKOWY-22\",\"quantity\":1}"), 3,
            CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Api.Unauthorized", ErrorType.Unauthorized)));
    }

    // Nieznany typ – błąd bez wysyłania żądania
    [Test]
    public async Task Send_UnknownType_ReturnsUnexpectedWithoutRequest()
    {
        _stub.Respond(HttpStatusCode.OK, VisitDetailsJson(4));

        var result = await _gateway.SendAsync(Message("VisitTeleported", "{}"), 3, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Api.Unexpected", ErrorType.Unexpected)));
        Assert.That(_stub.Requests, Is.Empty);
    }

    // Wyjątek sieci → Network
    [Test]
    public async Task Send_HttpRequestException_ReturnsNetwork()
    {
        _stub.Throw(new HttpRequestException("brak sieci"));

        var result = await _gateway.SendAsync(Message("VisitStarted", "{\"startedAt\":\"2026-10-30T08:15:00+00:00\"}"), 3,
            CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }
}
