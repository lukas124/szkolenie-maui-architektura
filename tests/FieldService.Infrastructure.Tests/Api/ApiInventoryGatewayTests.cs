using FieldService.Infrastructure.Inventory;
using System.Net;
using FieldService.Application.Parts.Commands;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Api;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Api;

[TestFixture]
public class ApiInventoryGatewayTests
{
    private static readonly Guid VisitGuid = Guid.Parse("6f1c2a3b-4d5e-4f60-8a7b-9c0d1e2f3a4b");
    private static readonly Guid ReservationGuid = Guid.Parse("0b9e8d7c-6a5b-4c3d-9e2f-1a0b9c8d7e6f");
    private static readonly DateTimeOffset ExpiresAt = new(2026, 10, 30, 14, 5, 0, TimeSpan.Zero);

    private StubHttpHandler _stub = null!;
    private ApiInventoryGateway _gateway = null!;

    [TearDown]

    public void TearDown() => _stub.Dispose();


    [SetUp]
    public void SetUp()
    {
        _stub = new StubHttpHandler();
        var client = new FieldServiceApiClient(new HttpClient(_stub) { BaseAddress = new Uri("https://api.test/") });
        _gateway = new ApiInventoryGateway(client, new ApiExecutor(NullLogger<ApiExecutor>.Instance));
    }

    private static ReservePart Command() => new(VisitGuid, "KOMPRESOR-R32", "MAG-01", 2);

    private static string ReservationJson() =>
        $"{{\"id\":\"{ReservationGuid}\",\"partId\":\"KOMPRESOR-R32\",\"warehouseId\":\"MAG-01\"," +
        $"\"visitId\":\"{VisitGuid}\",\"quantity\":2,\"expiresAt\":\"2026-10-30T14:05:00+00:00\"}}";

    // 201 → rezerwacja z odpowiedzi (Id rezerwacji z serwera)
    [Test]
    [Ignore("Ćwiczenie 13: ApiInventoryGateway.ReserveAsync")]
    public async Task Reserve_Success_ReturnsReservationId()
    {
        _stub.Respond(HttpStatusCode.Created, ReservationJson());

        var result = await _gateway.ReserveAsync(Command(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Id, Is.EqualTo(ReservationGuid));
    }

    // Ktoś był szybszy – 409 z kodem Inventory.OutOfStock
    [Test]
    [Ignore("Ćwiczenie 13: ApiInventoryGateway.ReserveAsync")]
    public async Task Reserve_409_ReturnsConflictOutOfStock()
    {
        _stub.Respond(HttpStatusCode.Conflict,
            "{\"type\":\"about:blank\",\"title\":\"Conflict\",\"status\":409,\"code\":\"Inventory.OutOfStock\"}",
            "application/problem+json");

        var result = await _gateway.ReserveAsync(Command(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        // równość rekordu Error = ten sam kod i typ (Conflict)
        Assert.That(result.Error, Is.EqualTo(InventoryErrors.OutOfStock));
    }

    // Brak sieci → Network
    [Test]
    [Ignore("Ćwiczenie 13: ApiInventoryGateway.ReserveAsync")]
    public async Task Reserve_HttpRequestException_ReturnsNetwork()
    {
        _stub.Throw(new HttpRequestException("brak sieci"));

        var result = await _gateway.ReserveAsync(Command(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // Pełne mapowanie odpowiedzi na PartReservation
    [Test]
    [Ignore("Ćwiczenie 13: ApiInventoryGateway.ReserveAsync")]
    public async Task Reserve_Success_MapsAllFields()
    {
        _stub.Respond(HttpStatusCode.Created, ReservationJson());

        var result = await _gateway.ReserveAsync(Command(), CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(new PartReservation(ReservationGuid, "KOMPRESOR-R32", "MAG-01", 2, ExpiresAt)));
    }

    // Żądanie: POST api/parts/reservations z polami partId, warehouseId, visitId, quantity
    [Test]
    [Ignore("Ćwiczenie 13: ApiInventoryGateway.ReserveAsync")]
    public async Task Reserve_SendsPostWithFields()
    {
        _stub.Respond(HttpStatusCode.Created, ReservationJson());

        await _gateway.ReserveAsync(Command(), CancellationToken.None);

        var request = _stub.LastRequest;
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.Path, Does.EndWith("/api/parts/reservations"));
        var body = request.Json();
        Assert.That(body.GetProperty("partId").GetString(), Is.EqualTo("KOMPRESOR-R32"));
        Assert.That(body.GetProperty("warehouseId").GetString(), Is.EqualTo("MAG-01"));
        Assert.That(body.GetProperty("visitId").GetGuid(), Is.EqualTo(VisitGuid));
        Assert.That(body.GetProperty("quantity").GetInt32(), Is.EqualTo(2));
    }

    // Jedno żądanie na rezerwację (POST bez automatycznego ponawiania po stronie bramy)
    [Test]
    [Ignore("Ćwiczenie 13: ApiInventoryGateway.ReserveAsync")]
    public async Task Reserve_409_SendsSingleRequest()
    {
        _stub.Respond(HttpStatusCode.Conflict, "{\"status\":409,\"code\":\"Inventory.OutOfStock\"}");

        await _gateway.ReserveAsync(Command(), CancellationToken.None);

        Assert.That(_stub.Requests, Has.Count.EqualTo(1));
    }

    // 404 (nieznana część) z kodem z treści
    [Test]
    [Ignore("Ćwiczenie 13: ApiInventoryGateway.ReserveAsync")]
    public async Task Reserve_404_ReturnsNotFoundWithCode()
    {
        _stub.Respond(HttpStatusCode.NotFound, "{\"status\":404,\"code\":\"Inventory.PartNotFound\"}");

        var result = await _gateway.ReserveAsync(Command(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Inventory.PartNotFound", ErrorType.NotFound)));
    }

    // GET api/warehouses → lista magazynów (id, name)
    [Test]
    public async Task GetWarehouses_Success_MapsWarehouses()
    {
        _stub.Respond(HttpStatusCode.OK,
            "[{\"id\":\"MAG-01\",\"name\":\"Magazyn Centralny\"},{\"id\":\"MAG-02\",\"name\":\"Magazyn Południe\"}]");

        var result = await _gateway.GetWarehousesAsync(CancellationToken.None);

        Assert.That(_stub.LastRequest.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(_stub.LastRequest.Path, Does.EndWith("/api/warehouses"));
        Assert.That(result.Value, Is.EqualTo(new[]
        {
            new Warehouse("MAG-01", "Magazyn Centralny"),
            new Warehouse("MAG-02", "Magazyn Południe"),
        }));
    }

    // 401 przy liście magazynów → Api.Unauthorized
    [Test]
    public async Task GetWarehouses_401_ReturnsUnauthorized()
    {
        _stub.Respond(HttpStatusCode.Unauthorized);

        var result = await _gateway.GetWarehousesAsync(CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Api.Unauthorized", ErrorType.Unauthorized)));
    }

    // GET api/vanstock → VanStockItem(PartId, Name = partName, Quantity)
    [Test]
    public async Task GetVanStock_Success_MapsItems()
    {
        _stub.Respond(HttpStatusCode.OK,
            "[{\"partId\":\"FILTR-SIATKOWY-22\",\"partName\":\"Filtr siatkowy 22 mm\",\"quantity\":3," +
            "\"thumbnailUrl\":\"https://api.test/api/images/filtr?size=thumb\"}," +
            "{\"partId\":\"USZCZELKA-15\",\"partName\":\"Uszczelka 15 mm\",\"quantity\":10," +
            "\"thumbnailUrl\":\"https://api.test/api/images/uszczelka?size=thumb\"}]");

        var result = await _gateway.GetVanStockAsync(CancellationToken.None);

        Assert.That(_stub.LastRequest.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(_stub.LastRequest.Path, Does.EndWith("/api/vanstock"));
        Assert.That(result.Value, Is.EquivalentTo(new[]
        {
            new VanStockItem("FILTR-SIATKOWY-22", "Filtr siatkowy 22 mm", 3),
            new VanStockItem("USZCZELKA-15", "Uszczelka 15 mm", 10),
        }));
    }

    // GET api/parts → CatalogPart(PartId = id, Name, UnitPrice)
    [Test]
    public async Task GetPartCatalog_Success_MapsParts()
    {
        _stub.Respond(HttpStatusCode.OK,
            "[{\"id\":\"KOMPRESOR-R32\",\"name\":\"Kompresor R32\",\"description\":\"Kompresor do pomp ciepła\"," +
            "\"unitPrice\":1899.5,\"imageUrl\":\"https://api.test/api/images/kompresor\"," +
            "\"thumbnailUrl\":\"https://api.test/api/images/kompresor?size=thumb\"}," +
            "{\"id\":\"FILTR-SIATKOWY-22\",\"name\":\"Filtr siatkowy 22 mm\",\"description\":\"Filtr\"," +
            "\"unitPrice\":49.99,\"imageUrl\":\"https://api.test/api/images/filtr\"," +
            "\"thumbnailUrl\":\"https://api.test/api/images/filtr?size=thumb\"}]");

        var result = await _gateway.GetPartCatalogAsync(CancellationToken.None);

        Assert.That(_stub.LastRequest.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(_stub.LastRequest.Path, Does.EndWith("/api/parts"));
        Assert.That(result.Value, Is.EquivalentTo(new[]
        {
            new CatalogPart("KOMPRESOR-R32", "Kompresor R32", 1899.5m),
            new CatalogPart("FILTR-SIATKOWY-22", "Filtr siatkowy 22 mm", 49.99m),
        }));
    }

    // Brak sieci przy katalogu → Network
    [Test]
    public async Task GetPartCatalog_HttpRequestException_ReturnsNetwork()
    {
        _stub.Throw(new HttpRequestException("brak sieci"));

        var result = await _gateway.GetPartCatalogAsync(CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }
}
