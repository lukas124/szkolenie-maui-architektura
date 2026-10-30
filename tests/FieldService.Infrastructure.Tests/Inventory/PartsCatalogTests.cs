using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common; // ZAŁOŻENIE: Result<T>, Error, ErrorType w tej przestrzeni nazw
using FieldService.Infrastructure.Api;
using FieldService.Infrastructure.Inventory;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Tests.Caching;
using FieldService.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Inventory;

[TestFixture]
public sealed class PartsCatalogTests
{
    private static readonly IReadOnlyList<CatalogPart> ThreeParts =
    [
        new("P-FILTR-01", "Filtr siatkowy", 25m),
        new("P-POMPA-02", "Pompa obiegowa", 480m),
        new("P-ZAWOR-03", "Zawór trójdrogowy", 210.5m),
    ];

    private static readonly IReadOnlyList<CatalogPart> TwoParts =
    [
        new("P-CZUJNIK-04", "Czujnik temperatury", 65m),
        new("P-USZCZ-05", "Uszczelka", 3.5m),
    ];

    private TemporaryAppPaths _paths = null!;
    private SqliteDb _db = null!;
    private Mock<IInventoryGateway> _gateway = null!;
    private Mock<INetworkStatus> _network = null!;
    private readonly List<MemoryCache> _memoryCaches = [];

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _db = new SqliteDb(_paths);
        _gateway = new Mock<IInventoryGateway>();
        _network = new Mock<INetworkStatus>();
        _network.Setup(n => n.IsOnline).Returns(true);
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var memory in _memoryCaches)
        {
            memory.Dispose();
        }
        _memoryCaches.Clear();
        await CacheTestHelpers.DisposeDbAsync(_db);
        _paths.Dispose();
    }

    private PartsCatalog CreateCatalog()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        _memoryCaches.Add(memory);
        return new PartsCatalog(_gateway.Object, _network.Object, _db, memory, new CachingOptions());
    }

    private void GatewayReturns(params Result<IReadOnlyList<CatalogPart>>[] results)
    {
        var sequence = _gateway.SetupSequence(g => g.GetPartCatalogAsync(It.IsAny<CancellationToken>()));
        foreach (var result in results)
        {
            sequence = sequence.ReturnsAsync(result);
        }
    }

    private static Result<IReadOnlyList<CatalogPart>> Ok(IReadOnlyList<CatalogPart> parts) =>
        Result<IReadOnlyList<CatalogPart>>.Success(parts);

    private static Result<IReadOnlyList<CatalogPart>> Fail(Error error) =>
        Result<IReadOnlyList<CatalogPart>>.Failure(error);

    // Pierwsze pobranie idzie do API i zwraca katalog z bramy
    [Test]
    public async Task GetAsync_FirstCall_ReturnsCatalogFromGateway()
    {
        GatewayReturns(Ok(ThreeParts));
        var catalog = CreateCatalog();

        var result = await catalog.GetAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EquivalentTo(ThreeParts));
        _gateway.Verify(g => g.GetPartCatalogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // W czasie TTL kolejne wywołania obsługuje pamięć – jedno żądanie do API
    [Test]
    public async Task GetAsync_CalledTenTimesWithinTtl_CallsGatewayOnce()
    {
        GatewayReturns(Ok(ThreeParts));
        var catalog = CreateCatalog();

        for (var i = 0; i < 10; i++)
        {
            var result = await catalog.GetAsync(CancellationToken.None);
            Assert.That(result.IsSuccess, Is.True);
        }

        _gateway.Verify(g => g.GetPartCatalogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Udane pobranie zapisuje katalog w tabeli parts_catalog
    [Test]
    public async Task GetAsync_GatewaySucceeds_StoresCatalogInSqlite()
    {
        GatewayReturns(Ok(ThreeParts));
        var catalog = CreateCatalog();

        await catalog.GetAsync(CancellationToken.None);

        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "parts_catalog"), Is.EqualTo(3));
    }

    // Clear() czyści pamięć – następne wywołanie idzie do API
    [Test]
    public async Task Clear_AfterGet_NextGetCallsGatewayAgain()
    {
        GatewayReturns(Ok(ThreeParts), Ok(TwoParts));
        var catalog = CreateCatalog();
        await catalog.GetAsync(CancellationToken.None);

        catalog.Clear();
        var result = await catalog.GetAsync(CancellationToken.None);

        Assert.That(result.Value, Is.EquivalentTo(TwoParts));
        _gateway.Verify(g => g.GetPartCatalogAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // Kolejne udane pobranie zastępuje całą zawartość tabeli (bez dopisywania)
    [Test]
    public async Task GetAsync_SecondSuccessfulFetch_ReplacesWholeTable()
    {
        GatewayReturns(Ok(ThreeParts), Ok(TwoParts));
        var catalog = CreateCatalog();
        await catalog.GetAsync(CancellationToken.None);
        catalog.Clear();

        await catalog.GetAsync(CancellationToken.None);

        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "parts_catalog"), Is.EqualTo(2));
    }

    // Brak sieci i katalog w SQLite → katalog z SQLite (ten sam obiekt po wyczyszczeniu pamięci)
    [Test]
    public async Task GetAsync_NetworkErrorAndCatalogInSqlite_ReturnsCatalogFromSqlite()
    {
        GatewayReturns(Ok(ThreeParts), Fail(ApiErrors.Network));
        var catalog = CreateCatalog();
        await catalog.GetAsync(CancellationToken.None);
        catalog.Clear();

        var result = await catalog.GetAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EquivalentTo(ThreeParts));
    }

    // „Po ubiciu procesu”: nowy obiekt PartsCatalog (pusta pamięć) na tej samej bazie bez sieci → katalog z SQLite
    [Test]
    public async Task GetAsync_NewInstanceOnSameDatabaseWithoutNetwork_ReturnsCatalogFromSqlite()
    {
        GatewayReturns(Ok(ThreeParts), Fail(ApiErrors.Network));
        await CreateCatalog().GetAsync(CancellationToken.None);

        var afterRestart = CreateCatalog();
        var result = await afterRestart.GetAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EquivalentTo(ThreeParts));
    }

    // Telefon bez internetu → katalog od razu z SQLite, bez wywołania API (bez żądania HTTP i ponowień)
    [Test]
    public async Task GetAsync_Offline_ReturnsCatalogFromSqliteWithoutCallingGateway()
    {
        GatewayReturns(Ok(ThreeParts));
        await CreateCatalog().GetAsync(CancellationToken.None);
        _network.Setup(n => n.IsOnline).Returns(false);

        var result = await CreateCatalog().GetAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EquivalentTo(ThreeParts));
        _gateway.Verify(g => g.GetPartCatalogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Brak sieci i pusta tabela → ten sam błąd Network
    [Test]
    public async Task GetAsync_NetworkErrorAndEmptySqlite_ReturnsNetworkError()
    {
        GatewayReturns(Fail(ApiErrors.Network));
        var catalog = CreateCatalog();

        var result = await catalog.GetAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // Błąd inny niż Network → ten sam błąd, bez sięgania do SQLite (mimo zapisanego katalogu)
    [Test]
    public async Task GetAsync_NonNetworkErrorAndCatalogInSqlite_ReturnsSameError()
    {
        var unexpected = new Error("Api.Unexpected", ErrorType.Unexpected);
        GatewayReturns(Ok(ThreeParts), Fail(unexpected));
        var catalog = CreateCatalog();
        await catalog.GetAsync(CancellationToken.None);
        catalog.Clear();

        var result = await catalog.GetAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(unexpected));
    }

    // Błąd API nie usuwa katalogu zapisanego w SQLite
    [Test]
    public async Task GetAsync_GatewayError_KeepsSqliteRows()
    {
        GatewayReturns(Ok(ThreeParts), Fail(new Error("Api.Unexpected", ErrorType.Unexpected)));
        var catalog = CreateCatalog();
        await catalog.GetAsync(CancellationToken.None);
        catalog.Clear();

        await catalog.GetAsync(CancellationToken.None);

        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "parts_catalog"), Is.EqualTo(3));
    }
}
