using System.Net;
using FieldService.Infrastructure.Http;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Tests.Api;
using FieldService.Infrastructure.Tests.Caching;
using FieldService.Infrastructure.Tests.Persistence;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Http;

[TestFixture]
public sealed class ETagCacheHandlerTests
{
    private const string VisitsUrl = "https://api.test/api/visits/today";
    private const string VisitsJson = "[{\"id\":\"1\"}]";
    private const string NewVisitsJson = "[{\"id\":\"1\"},{\"id\":\"2\"}]";

    private TemporaryAppPaths _paths = null!;
    private SqliteDb _db = null!;
    private StubHttpHandler _stub = null!;
    private HttpMessageInvoker _invoker = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _db = new SqliteDb(_paths);
        _stub = new StubHttpHandler();
        _invoker = new HttpMessageInvoker(new ETagCacheHandler(_db) { InnerHandler = _stub });
    }

    [TearDown]
    public async Task TearDown()
    {
        _invoker.Dispose();
        _stub.Dispose();
        await CacheTestHelpers.DisposeDbAsync(_db);
        _paths.Dispose();
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url = VisitsUrl) =>
        _invoker.SendAsync(new HttpRequestMessage(method, url), CancellationToken.None);

    private Task<HttpResponseMessage> GetAsync(string url = VisitsUrl) => SendAsync(HttpMethod.Get, url);

    // Pierwszy GET (pusty cache) idzie bez If-None-Match
    [Test]
    public async Task SendAsync_FirstGet_SendsNoIfNoneMatch()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson, etag: "\"v7\"");

        await GetAsync();

        Assert.That(_stub.LastRequest.Header("If-None-Match"), Is.Null);
    }

    // GET z zapamiętanym ETagiem → żądanie z If-None-Match: "v7"
    [Test]
    public async Task SendAsync_GetWithStoredETag_SendsIfNoneMatch()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson, etag: "\"v7\"");
        await GetAsync();

        await GetAsync();

        Assert.That(_stub.Requests, Has.Count.EqualTo(2));
        Assert.That(_stub.LastRequest.Header("If-None-Match"), Is.EqualTo("\"v7\""));
    }

    // 304 z serwera → klient dostaje 200 z zapamiętaną treścią, Content-Type application/json i nagłówkiem ETag
    [Test]
    public async Task SendAsync_ServerReturnsNotModified_ReturnsOkWithStoredBody()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson, etag: "\"v7\"");
        _stub.Respond(HttpStatusCode.NotModified);
        await GetAsync();

        using var response = await GetAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(VisitsJson));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        Assert.That(response.Headers.ETag?.Tag, Is.EqualTo("\"v7\""));
    }

    // 200 z nowym ETagiem zastępuje wpis: kolejne żądanie wysyła nowy ETag, a 304 zwraca nową treść
    [Test]
    public async Task SendAsync_OkWithNewETag_ReplacesStoredEntry()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson, etag: "\"v7\"");
        _stub.Respond(HttpStatusCode.OK, NewVisitsJson, etag: "\"v8\"");
        _stub.Respond(HttpStatusCode.NotModified);
        await GetAsync();
        await GetAsync();

        using var response = await GetAsync();

        Assert.That(_stub.LastRequest.Header("If-None-Match"), Is.EqualTo("\"v8\""));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(NewVisitsJson));
        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "http_cache"), Is.EqualTo(1));
    }

    // Kluczem jest pełny adres – inny adres (inne zapytanie) nie dostaje If-None-Match
    [Test]
    public async Task SendAsync_DifferentUrl_SendsNoIfNoneMatch()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson, etag: "\"v7\"");
        await GetAsync("https://api.test/api/visits/history?page=1&pageSize=30");

        await GetAsync("https://api.test/api/visits/history?page=2&pageSize=30");

        Assert.That(_stub.LastRequest.Header("If-None-Match"), Is.Null);
    }

    // 200 bez ETagu → nic nie zapisane
    [Test]
    public async Task SendAsync_OkWithoutETag_StoresNothing()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson);
        await GetAsync();

        await GetAsync();

        Assert.That(_stub.LastRequest.Header("If-None-Match"), Is.Null);
        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "http_cache"), Is.EqualTo(0));
    }

    // Odpowiedź z błędem (5xx) z ETagiem → nic nie zapisane
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.NotFound)]
    public async Task SendAsync_ErrorStatusWithETag_StoresNothing(HttpStatusCode status)
    {
        _stub.Respond(status, "{\"status\":500}", etag: "\"v7\"");

        await GetAsync();

        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "http_cache"), Is.EqualTo(0));
    }

    // Błąd po zapisanym wpisie nie usuwa go – następny GET nadal wysyła stary ETag
    [Test]
    public async Task SendAsync_ErrorAfterStoredEntry_KeepsOldEntry()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson, etag: "\"v7\"");
        _stub.Respond(HttpStatusCode.InternalServerError);
        await GetAsync();
        await GetAsync();

        await GetAsync();

        Assert.That(_stub.LastRequest.Header("If-None-Match"), Is.EqualTo("\"v7\""));
        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "http_cache"), Is.EqualTo(1));
    }

    // Metody inne niż GET nie są zapamiętywane, nawet z nagłówkiem ETag
    [Test]
    public async Task SendAsync_PostWithETag_StoresNothing()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson, etag: "\"v7\"");

        await SendAsync(HttpMethod.Post);

        Assert.That(await CacheTestHelpers.CountRowsAsync(_db, "http_cache"), Is.EqualTo(0));
    }

    // Wpis jest trwały: nowy handler na tej samej bazie (po restarcie aplikacji) wysyła If-None-Match
    [Test]
    public async Task SendAsync_NewHandlerOnSameDatabase_SendsStoredETag()
    {
        _stub.Respond(HttpStatusCode.OK, VisitsJson, etag: "\"v7\"");
        await GetAsync();
        var secondStub = new StubHttpHandler();
        secondStub.Respond(HttpStatusCode.NotModified);
        using var secondInvoker = new HttpMessageInvoker(new ETagCacheHandler(_db) { InnerHandler = secondStub });

        using var response = await secondInvoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, VisitsUrl), CancellationToken.None);

        Assert.That(secondStub.LastRequest.Header("If-None-Match"), Is.EqualTo("\"v7\""));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(VisitsJson));
    }
}
