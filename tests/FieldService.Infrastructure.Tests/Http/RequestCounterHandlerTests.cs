using System.Net;
using FieldService.Application.Abstractions;
using FieldService.Infrastructure.Http;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Tests.Api;
using FieldService.Infrastructure.Tests.Caching;
using FieldService.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Http;

[TestFixture]
public sealed class RequestCounterHandlerTests
{
    private const string Url = "https://api.test/api/parts/catalog";

    private RequestStatistics _stats = null!;
    private StubHttpHandler _stub = null!;
    private HttpMessageInvoker _invoker = null!;

    [SetUp]
    public void SetUp()
    {
        _stats = new RequestStatistics();
        _stub = new StubHttpHandler();
        _invoker = new HttpMessageInvoker(CreateCounter(_stub));
    }

    [TearDown]
    public void TearDown()
    {
        _invoker.Dispose();
        _stub.Dispose();
    }

    private RequestCounterHandler CreateCounter(HttpMessageHandler inner) =>
        new(_stats, NullLogger<RequestCounterHandler>.Instance) { InnerHandler = inner };

    private async Task GetAsync()
    {
        using var response = await _invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, Url), CancellationToken.None);
    }

    // Nowe statystyki są wyzerowane
    [Test]
    public void Snapshot_NoRequests_ReturnsZeros()
    {
        Assert.That(_stats.Snapshot(), Is.EqualTo(new RequestStats(0, 0, 0, 0)));
    }

    // Odpowiedź 200 liczy się do Total i Ok, bajty z długości treści
    [Test]
    public async Task SendAsync_OkResponse_CountsTotalOkAndBytes()
    {
        _stub.Respond(HttpStatusCode.OK, "{\"a\":1}"); // 7 bajtów

        await GetAsync();

        Assert.That(_stats.Snapshot(), Is.EqualTo(new RequestStats(1, 1, 0, 7)));
    }

    // Odpowiedź 304 liczy się do Total i NotModified
    [Test]
    public async Task SendAsync_NotModifiedResponse_CountsNotModified()
    {
        _stub.Respond(HttpStatusCode.NotModified);

        await GetAsync();

        var stats = _stats.Snapshot();
        Assert.That(stats.Total, Is.EqualTo(1));
        Assert.That(stats.Ok, Is.EqualTo(0));
        Assert.That(stats.NotModified, Is.EqualTo(1));
    }

    // Błąd serwera liczy się tylko do Total
    [Test]
    public async Task SendAsync_ServerError_CountsOnlyTotal()
    {
        _stub.Respond(HttpStatusCode.InternalServerError);

        await GetAsync();

        var stats = _stats.Snapshot();
        Assert.That(stats.Total, Is.EqualTo(1));
        Assert.That(stats.Ok, Is.EqualTo(0));
        Assert.That(stats.NotModified, Is.EqualTo(0));
    }

    // Kilka odpowiedzi: liczniki i bajty się sumują
    [Test]
    public async Task SendAsync_MixedResponses_AccumulatesCounters()
    {
        _stub.Respond(HttpStatusCode.OK, "{\"a\":1}");
        _stub.Respond(HttpStatusCode.NotModified);
        _stub.Respond(HttpStatusCode.OK, "[1,2,3]");

        await GetAsync();
        await GetAsync();
        await GetAsync();

        Assert.That(_stats.Snapshot(), Is.EqualTo(new RequestStats(3, 2, 1, 14)));
    }

    // Reset() zeruje wszystkie liczniki
    [Test]
    public async Task Reset_AfterRequests_ZeroesCounters()
    {
        _stub.Respond(HttpStatusCode.OK, "{\"a\":1}");
        await GetAsync();

        _stats.Reset();

        Assert.That(_stats.Snapshot(), Is.EqualTo(new RequestStats(0, 0, 0, 0)));
    }

    // Statystyki są bezpieczne wątkowo – 100 równoległych żądań daje Total = 100
    [Test]
    public async Task SendAsync_ParallelRequests_CountsAll()
    {
        _stub.Respond(HttpStatusCode.OK, "{}");

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(GetAsync)));

        var stats = _stats.Snapshot();
        Assert.That(stats.Total, Is.EqualTo(100));
        Assert.That(stats.Ok, Is.EqualTo(100));
        Assert.That(stats.Bytes, Is.EqualTo(200));
    }

    // RequestStatistics jest dostępne przez port IRequestStatistics
    [Test]
    public void RequestStatistics_ImplementsPort()
    {
        Assert.That(_stats, Is.InstanceOf<IRequestStatistics>());
    }

    // Kolejność w potoku: licznik ZA ETagCacheHandler widzi prawdziwe 304, choć wywołujący dostaje 200
    [Test]
    public async Task SendAsync_BehindETagHandler_SeesRealNotModified()
    {
        var paths = new TemporaryAppPaths();
        var db = new SqliteDb(paths);
        try
        {
            var stub = new StubHttpHandler();
            stub.Respond(HttpStatusCode.OK, "{\"a\":1}", etag: "\"v7\"");
            stub.Respond(HttpStatusCode.NotModified);
            using var invoker = new HttpMessageInvoker(new ETagCacheHandler(db) { InnerHandler = CreateCounter(stub) });

            using var first = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, Url), CancellationToken.None);
            using var second = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, Url), CancellationToken.None);

            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var stats = _stats.Snapshot();
            Assert.That(stats.Total, Is.EqualTo(2));
            Assert.That(stats.Ok, Is.EqualTo(1));
            Assert.That(stats.NotModified, Is.EqualTo(1));
        }
        finally
        {
            await CacheTestHelpers.DisposeDbAsync(db);
            paths.Dispose();
        }
    }
}
