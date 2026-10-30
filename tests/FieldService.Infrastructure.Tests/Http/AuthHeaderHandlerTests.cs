using System.Net;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Http;
using FieldService.Infrastructure.Tests.Api;
using Moq;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Http;

[TestFixture]
public class AuthHeaderHandlerTests
{
    private static readonly Error Unauthorized = new("Api.Unauthorized", ErrorType.Unauthorized);

    private StubHttpHandler _stub = null!;
    private Mock<IAuthGateway> _auth = null!;
    private Mock<ISessionStore> _session = null!;
    private Mock<ISessionExpiry> _expiry = null!;
    private AccessTokenStore _tokens = null!;
    private HttpMessageInvoker _invoker = null!;

    [SetUp]
    public void SetUp()
    {
        _stub = new StubHttpHandler();
        _auth = new Mock<IAuthGateway>();
        _session = new Mock<ISessionStore>();
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh-1");
        _expiry = new Mock<ISessionExpiry>();
        _tokens = new AccessTokenStore();

        var refresher = new TokenRefresher(_auth.Object, _session.Object, _tokens, _expiry.Object);
        var handler = new AuthHeaderHandler(_tokens, refresher) { InnerHandler = _stub };
        _invoker = new HttpMessageInvoker(handler);
    }

    [TearDown]
    public void TearDown()
    {
        _invoker.Dispose();
        _stub.Dispose();
    }

    private Task<HttpResponseMessage> SendAsync() =>
        _invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.test/api/visits/today"), CancellationToken.None);

    private void RefreshSucceeds() =>
        _auth.Setup(a => a.RefreshAsync("refresh-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Success(new SessionTokens("access-2", "refresh-2", "Jan Kowalski")));

    // Token w nagłówku Authorization: Bearer {token}
    [Test]
    public async Task Send_WithToken_AddsBearerHeader()
    {
        _tokens.AccessToken = "access-1";
        _stub.Respond(HttpStatusCode.OK, "[]");

        await SendAsync();

        Assert.That(_stub.LastRequest.Header("Authorization"), Is.EqualTo("Bearer access-1"));
    }

    // Brak tokenu – bez nagłówka Authorization
    [Test]
    public async Task Send_WithoutToken_SendsNoAuthorizationHeader()
    {
        _tokens.AccessToken = null;
        _stub.Respond(HttpStatusCode.OK, "[]");

        await SendAsync();

        Assert.That(_stub.LastRequest.Header("Authorization"), Is.Null);
    }

    // 401 → odświeżenie → jedno ponowienie z nowym tokenem
    [Test]
    public async Task Send_401ThenRefreshSucceeds_RetriesOnceWithNewToken()
    {
        _tokens.AccessToken = "access-1";
        RefreshSucceeds();
        _stub.Respond(HttpStatusCode.Unauthorized).Respond(HttpStatusCode.OK, "[]");

        var response = await SendAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_stub.Requests, Has.Count.EqualTo(2));
        Assert.That(_stub.Requests[0].Header("Authorization"), Is.EqualTo("Bearer access-1"));
        Assert.That(_stub.Requests[1].Header("Authorization"), Is.EqualTo("Bearer access-2"));
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Ponowienie tego samego żądania: ta sama metoda i ścieżka
    [Test]
    public async Task Send_401ThenRefreshSucceeds_RetriesSameRequest()
    {
        _tokens.AccessToken = "access-1";
        RefreshSucceeds();
        _stub.Respond(HttpStatusCode.Unauthorized).Respond(HttpStatusCode.OK, "[]");

        await SendAsync();

        Assert.That(_stub.Requests[1].Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(_stub.Requests[1].Path, Is.EqualTo(_stub.Requests[0].Path));
    }

    // Odświeżenie nieudane (odmowa) → 401 dalej, bez ponowienia
    [Test]
    public async Task Send_401ThenRefreshRejected_Returns401WithoutRetry()
    {
        _tokens.AccessToken = "access-1";
        _auth.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Failure(Unauthorized));
        _stub.Respond(HttpStatusCode.Unauthorized);

        var response = await SendAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_stub.Requests, Has.Count.EqualTo(1));
        _expiry.Verify(e => e.OnExpired(), Times.Once);
    }

    // Odświeżenie nieudane przez brak sieci (sesja żyje) → błąd sieci, nie „zaloguj się”; bez ponowienia
    [Test]
    public void Send_401ThenRefreshNetworkError_ThrowsHttpRequestExceptionWithoutRetry()
    {
        _tokens.AccessToken = "access-1";
        _auth.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Failure(new Error("Api.Network", ErrorType.Network)));
        _stub.Respond(HttpStatusCode.Unauthorized);

        Assert.ThrowsAsync<HttpRequestException>(SendAsync);
        Assert.That(_stub.Requests, Has.Count.EqualTo(1));
        Assert.That(_tokens.AccessToken, Is.EqualTo("access-1"));
    }

    // Drugie 401 (po ponowieniu) wraca bez kolejnego odświeżania
    [Test]
    public async Task Send_401AfterRetry_ReturnsWithoutSecondRefresh()
    {
        _tokens.AccessToken = "access-1";
        RefreshSucceeds();
        _stub.Respond(HttpStatusCode.Unauthorized);

        var response = await SendAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_stub.Requests, Has.Count.EqualTo(2));
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Odpowiedź inna niż 401 – bez odświeżania
    [TestCase(HttpStatusCode.OK)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task Send_Non401_DoesNotRefresh(HttpStatusCode status)
    {
        _tokens.AccessToken = "access-1";
        _stub.Respond(status);

        var response = await SendAsync();

        Assert.That(response.StatusCode, Is.EqualTo(status));
        Assert.That(_stub.Requests, Has.Count.EqualTo(1));
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
