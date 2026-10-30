using System.Net;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common; // ZAŁOŻENIE: Result<T> w tej przestrzeni nazw
using FieldService.Infrastructure.Caching;
using FieldService.Infrastructure.Http;
using FieldService.Infrastructure.Inventory;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Tests.Api;
using FieldService.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Caching;

[TestFixture]
public sealed class LocalCachesTests
{
    private static readonly IReadOnlyList<CatalogPart> Parts =
    [
        new("P-FILTR-01", "Filtr siatkowy", 25m),
        new("P-POMPA-02", "Pompa obiegowa", 480m),
    ];

    private TemporaryAppPaths _paths = null!;
    private SqliteDb _db = null!;
    private MemoryCache _memory = null!;
    private Mock<IInventoryGateway> _gateway = null!;
    private PartsCatalog _catalog = null!;
    private StubHttpHandler _thumbnailStub = null!;
    private ThumbnailCache _thumbnails = null!;
    private LocalCaches _caches = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _db = new SqliteDb(_paths);
        _memory = new MemoryCache(new MemoryCacheOptions());
        _gateway = new Mock<IInventoryGateway>();
        _gateway.Setup(g => g.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Success(Parts));
        // ten sam IMemoryCache dla PartsCatalog i LocalCaches (LocalCaches czyści wpis PartsCatalog.MemoryKey)
        _catalog = new PartsCatalog(_gateway.Object, Mock.Of<INetworkStatus>(n => n.IsOnline), _db, _memory, new CachingOptions());
        _thumbnailStub = new StubHttpHandler();
        _thumbnailStub.RespondBytes(HttpStatusCode.OK, [1, 2, 3, 4]);
        _thumbnails = new ThumbnailCache(
            () => new HttpClient(_thumbnailStub, disposeHandler: false), _paths, new CachingOptions());
        _caches = new LocalCaches(_db, _memory, _thumbnails);
    }

    [TearDown]
    public async Task TearDown()
    {
        _memory.Dispose();
        _thumbnailStub.Dispose();
        await CacheTestHelpers.DisposeDbAsync(_db);
        _paths.Dispose();
    }

    // Wypełnia cache: 2 wiersze parts_catalog, 1 wiersz http_cache, 1 plik miniatury
    private async Task FillCachesAsync()
    {
        await _catalog.GetAsync(CancellationToken.None);

        var httpStub = new StubHttpHandler();
        httpStub.Respond(HttpStatusCode.OK, "[]", etag: "\"v1\"");
        using var invoker = new HttpMessageInvoker(new ETagCacheHandler(_db) { InnerHandler = httpStub });
        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://api.test/api/visits/today"), CancellationToken.None);

        await using var thumbnail = await _thumbnails.OpenAsync("https://api.test/api/images/a?size=thumb", CancellationToken.None);
    }

    // Puste cache → 0 wpisów
    [Test]
    public async Task CountAsync_NothingCached_ReturnsZero()
    {
        Assert.That(await _caches.CountAsync(CancellationToken.None), Is.EqualTo(0));
    }

    // CountAsync = wiersze parts_catalog + http_cache + visit_list_snapshot + pliki miniatur
    [Test]
    public async Task CountAsync_AfterFilling_ReturnsSumOfAllEntries()
    {
        await FillCachesAsync();

        Assert.That(await _caches.CountAsync(CancellationToken.None), Is.EqualTo(2 + 1 + 1));
    }

    // ClearAsync → CountAsync = 0, tabele cache puste, miniatury usunięte
    [Test]
    public async Task ClearAsync_AfterFilling_CountIsZero()
    {
        await FillCachesAsync();

        await _caches.ClearAsync(CancellationToken.None);

        Assert.That(await _caches.CountAsync(CancellationToken.None), Is.EqualTo(0));
        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "parts_catalog"), Is.EqualTo(0));
        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "http_cache"), Is.EqualTo(0));
        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "visit_list_snapshot"), Is.EqualTo(0));
        Assert.That(_thumbnails.Count, Is.EqualTo(0));
    }

    // ClearAsync czyści też katalog w pamięci – następne GetAsync idzie do API
    [Test]
    public async Task ClearAsync_AfterCatalogLoaded_NextCatalogGetCallsGateway()
    {
        await _catalog.GetAsync(CancellationToken.None);

        await _caches.ClearAsync(CancellationToken.None);
        await _catalog.GetAsync(CancellationToken.None);

        _gateway.Verify(g => g.GetPartCatalogAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
