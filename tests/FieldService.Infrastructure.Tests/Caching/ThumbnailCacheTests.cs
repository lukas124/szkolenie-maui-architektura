using System.Net;
using FieldService.Infrastructure.Caching;
using FieldService.Infrastructure.Tests.Api;
using FieldService.Infrastructure.Tests.Persistence;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Caching;

[TestFixture]
public sealed class ThumbnailCacheTests
{
    private const string UrlA = "https://api.test/api/images/a?size=thumb";
    private const string UrlB = "https://api.test/api/images/b?size=thumb";
    private const string UrlC = "https://api.test/api/images/c?size=thumb";

    private TemporaryAppPaths _paths = null!;
    private StubHttpHandler _stub = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _stub = new StubHttpHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _stub.Dispose();
        _paths.Dispose();
    }

    private static byte[] Png(int length, byte fill = 0x42) => Enumerable.Repeat(fill, length).ToArray();

    // Fabryka klienta wołana przy każdym pobraniu; disposeHandler: false – stub przeżywa zwolnienie klienta
    private ThumbnailCache CreateCache(long memoryLimitBytes = 50 * 1024 * 1024, StubHttpHandler? stub = null)
    {
        var handler = stub ?? _stub;
        return new ThumbnailCache(
            () => new HttpClient(handler, disposeHandler: false),
            _paths,
            new CachingOptions { ThumbnailMemoryLimitBytes = memoryLimitBytes });
    }

    private static async Task<byte[]> OpenBytesAsync(ThumbnailCache cache, string url)
    {
        await using var stream = await cache.OpenAsync(url, CancellationToken.None);
        return await CacheTestHelpers.ReadAllAsync(stream);
    }

    private string ThumbnailsDirectory => Path.Combine(_paths.CacheDirectory, "thumbnails");

    // Pierwsze otwarcie pobiera obraz GET-em pod podany adres i zwraca jego bajty
    [Test]
    public async Task OpenAsync_FirstCall_DownloadsImage()
    {
        var png = Png(64);
        _stub.RespondBytes(HttpStatusCode.OK, png);
        var cache = CreateCache();

        var bytes = await OpenBytesAsync(cache, UrlA);

        Assert.That(bytes, Is.EqualTo(png));
        Assert.That(_stub.Requests, Has.Count.EqualTo(1));
        Assert.That(_stub.LastRequest.Method.ToString(), Is.EqualTo("GET"));
        Assert.That(_stub.LastRequest.Path, Does.EndWith("api/images/a"));
    }

    // Pobrany obraz trafia do pliku – Count = 1
    [Test]
    public async Task OpenAsync_AfterDownload_StoresFile()
    {
        _stub.RespondBytes(HttpStatusCode.OK, Png(64));
        var cache = CreateCache();

        await OpenBytesAsync(cache, UrlA);

        Assert.That(cache.Count, Is.EqualTo(1));
        Assert.That(Directory.GetFiles(ThumbnailsDirectory), Has.Length.EqualTo(1));
    }

    // Trafienie w pamięci: bez sieci i bez pliku (pliki usunięte, a obraz nadal jest)
    [Test]
    public async Task OpenAsync_MemoryHit_UsesNeitherNetworkNorFile()
    {
        var png = Png(64);
        _stub.RespondBytes(HttpStatusCode.OK, png);
        var cache = CreateCache();
        await OpenBytesAsync(cache, UrlA);
        Directory.Delete(ThumbnailsDirectory, recursive: true);

        var bytes = await OpenBytesAsync(cache, UrlA);

        Assert.That(bytes, Is.EqualTo(png));
        Assert.That(_stub.Requests, Has.Count.EqualTo(1));
    }

    // Trafienie w pliku: nowy obiekt cache (pusta pamięć) czyta plik bez sieci
    [Test]
    public async Task OpenAsync_FileHitInNewInstance_DoesNotUseNetwork()
    {
        var png = Png(64);
        _stub.RespondBytes(HttpStatusCode.OK, png);
        await OpenBytesAsync(CreateCache(), UrlA);
        var offlineStub = new StubHttpHandler();
        offlineStub.Throw(new HttpRequestException("brak sieci"));
        var afterRestart = CreateCache(stub: offlineStub);

        var bytes = await OpenBytesAsync(afterRestart, UrlA);

        Assert.That(bytes, Is.EqualTo(png));
        Assert.That(offlineStub.Requests, Is.Empty);
    }

    // Brak sieci i brak w cache → Stream.Null, bez wyjątku, nic nie zapisane
    [Test]
    public async Task OpenAsync_NetworkError_ReturnsStreamNull()
    {
        _stub.Throw(new HttpRequestException("brak sieci"));
        var cache = CreateCache();

        var stream = await cache.OpenAsync(UrlA, CancellationToken.None);

        Assert.That(stream, Is.SameAs(Stream.Null));
        Assert.That(cache.Count, Is.EqualTo(0));
        Assert.That(cache.MemoryBytes, Is.EqualTo(0));
    }

    // Status inny niż 2xx → Stream.Null, nic nie zapisane; następne otwarcie próbuje sieci ponownie
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task OpenAsync_NonSuccessStatus_ReturnsStreamNullAndStoresNothing(HttpStatusCode status)
    {
        _stub.RespondBytes(status, Png(10));
        _stub.RespondBytes(HttpStatusCode.OK, Png(64));
        var cache = CreateCache();

        var stream = await cache.OpenAsync(UrlA, CancellationToken.None);
        Assert.That(stream, Is.SameAs(Stream.Null));
        Assert.That(cache.Count, Is.EqualTo(0));

        var bytes = await OpenBytesAsync(cache, UrlA);
        Assert.That(bytes, Has.Length.EqualTo(64));
        Assert.That(_stub.Requests, Has.Count.EqualTo(2));
    }

    // Różne adresy to różne pliki
    [Test]
    public async Task OpenAsync_TwoUrls_StoresTwoFiles()
    {
        _stub.RespondBytes(HttpStatusCode.OK, Png(64, 0x01));
        _stub.RespondBytes(HttpStatusCode.OK, Png(64, 0x02));
        var cache = CreateCache();

        var a = await OpenBytesAsync(cache, UrlA);
        var b = await OpenBytesAsync(cache, UrlB);

        Assert.That(cache.Count, Is.EqualTo(2));
        Assert.That(a[0], Is.EqualTo(0x01));
        Assert.That(b[0], Is.EqualTo(0x02));
    }

    // Wpis większy niż limit pamięci nie zostaje w pamięci (jest tylko na dysku – kolejne otwarcie bez sieci)
    [Test]
    public async Task OpenAsync_EntryLargerThanLimit_IsNotKeptInMemory()
    {
        var png = Png(150);
        _stub.RespondBytes(HttpStatusCode.OK, png);
        var cache = CreateCache(memoryLimitBytes: 100);

        await OpenBytesAsync(cache, UrlA);

        Assert.That(() => cache.MemoryBytes, Is.EqualTo(0).After(2000, 50));
        Assert.That(cache.Count, Is.EqualTo(1));
        Assert.That(await OpenBytesAsync(cache, UrlA), Is.EqualTo(png));
        Assert.That(_stub.Requests, Has.Count.EqualTo(1));
    }

    // Limit pamięci: suma rozmiarów wpisów w pamięci nie przekracza limitu (starsze wypierane albo nowe odrzucane)
    [Test]
    public async Task OpenAsync_MoreBytesThanLimit_MemoryStaysWithinLimit()
    {
        _stub.RespondBytes(HttpStatusCode.OK, Png(60));
        var cache = CreateCache(memoryLimitBytes: 100);

        await OpenBytesAsync(cache, UrlA);
        await OpenBytesAsync(cache, UrlB);
        await OpenBytesAsync(cache, UrlC);

        Assert.That(() => cache.MemoryBytes, Is.LessThanOrEqualTo(100).After(2000, 50));
        Assert.That(cache.Count, Is.EqualTo(3));
    }

    // Limit 0 = bez limitu: wszystkie wpisy zostają w pamięci
    [Test]
    public async Task OpenAsync_ZeroLimit_KeepsAllEntriesInMemory()
    {
        _stub.RespondBytes(HttpStatusCode.OK, Png(60));
        var cache = CreateCache(memoryLimitBytes: 0);

        await OpenBytesAsync(cache, UrlA);
        await OpenBytesAsync(cache, UrlB);
        await OpenBytesAsync(cache, UrlC);

        Assert.That(cache.MemoryBytes, Is.EqualTo(180));
    }

    // Dwa wiersze z tą samą miniaturą pobierają ją naraz: drugi wpis podmienia pierwszy w pamięci,
    // a spóźnione zgłoszenie usunięcia pierwszego nie kasuje rozmiaru drugiego
    [Test]
    public async Task OpenAsync_SameUrlTwiceAtOnce_CountsReplacedEntryOnce()
    {
        using var network = new BothRequestsAtOnceHandler(Png(64));
        var cache = new ThumbnailCache(
            () => new HttpClient(network, disposeHandler: false),
            _paths,
            new CachingOptions { ThumbnailMemoryLimitBytes = 50 * 1024 * 1024 });

        await Task.WhenAll(OpenBytesAsync(cache, UrlA), OpenBytesAsync(cache, UrlA));

        Assert.That(network.Requests, Is.EqualTo(2));
        Assert.That(cache.MemoryBytes, Is.EqualTo(64));
    }

    // Clear() usuwa pamięć i pliki – następne otwarcie idzie do sieci
    [Test]
    public async Task Clear_AfterDownloads_RemovesMemoryAndFiles()
    {
        _stub.RespondBytes(HttpStatusCode.OK, Png(64));
        var cache = CreateCache();
        await OpenBytesAsync(cache, UrlA);
        await OpenBytesAsync(cache, UrlB);

        cache.Clear();

        Assert.That(cache.Count, Is.EqualTo(0));
        Assert.That(cache.MemoryBytes, Is.EqualTo(0));
        await OpenBytesAsync(cache, UrlA);
        Assert.That(_stub.Requests, Has.Count.EqualTo(3));
    }

    // Anulowanie przez wywołującego to jedyny wyjątek, który wychodzi z OpenAsync
    [Test]
    public void OpenAsync_CancelledToken_Throws()
    {
        _stub.RespondBytes(HttpStatusCode.OK, Png(64));
        var cache = CreateCache();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await cache.OpenAsync(UrlA, cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    // Atrapa sieci: każde żądanie czeka, aż przyjdzie drugie – oba pobrania kończą się razem (bez opóźnień czasowych)
    private sealed class BothRequestsAtOnceHandler(byte[] png) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requests;

        public int Requests => _requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _requests) == 2) _both.SetResult();
            await _both.Task.WaitAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) };
        }
    }
}
