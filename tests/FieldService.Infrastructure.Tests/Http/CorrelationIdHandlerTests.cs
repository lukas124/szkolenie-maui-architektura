using System.Net;
using FieldService.Infrastructure.Http;
using FieldService.Infrastructure.Tests.Api;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Http;

[TestFixture]
public class CorrelationIdHandlerTests
{
    private StubHttpHandler _stub = null!;
    private HttpMessageInvoker _invoker = null!;

    [SetUp]
    public void SetUp()
    {
        _stub = new StubHttpHandler().Respond(HttpStatusCode.OK);
        _invoker = new HttpMessageInvoker(new CorrelationIdHandler { InnerHandler = _stub });
    }

    [TearDown]
    public void TearDown()
    {
        _invoker.Dispose();
        _stub.Dispose();
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage? request = null) =>
        _invoker.SendAsync(request ?? new HttpRequestMessage(HttpMethod.Get, "https://api.test/api/visits/today"),
            CancellationToken.None);

    // Nagłówek X-Correlation-Id z Guid w formacie "D"
    [Test]
    public async Task Send_AddsCorrelationIdGuid()
    {
        await SendAsync();

        var value = _stub.LastRequest.Header("X-Correlation-Id");
        Assert.That(Guid.TryParseExact(value, "D", out _), Is.True, $"Nagłówek: {value}");
    }

    // Każde żądanie dostaje inny identyfikator
    [Test]
    public async Task Send_TwoRequests_UseDifferentIds()
    {
        await SendAsync();
        await SendAsync();

        Assert.That(_stub.Requests[0].Header("X-Correlation-Id"),
            Is.Not.EqualTo(_stub.Requests[1].Header("X-Correlation-Id")));
    }

    // Istniejący nagłówek zostaje nadpisany (jedna wartość, nowy Guid)
    [Test]
    public async Task Send_ExistingHeader_IsOverwritten()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/api/visits/today");
        request.Headers.Add("X-Correlation-Id", "stary-identyfikator");

        await SendAsync(request);

        var value = _stub.LastRequest.Header("X-Correlation-Id");
        Assert.That(Guid.TryParseExact(value, "D", out _), Is.True, $"Nagłówek: {value}");
    }
}
