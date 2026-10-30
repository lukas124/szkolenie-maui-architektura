using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Session;

[TestFixture]
public class TokenRefresherTests
{
    private static readonly SessionTokens NewTokens = new("access-2", "refresh-2", "Jan Kowalski");

    private Mock<IAuthGateway> _auth = null!;
    private Mock<ISessionStore> _session = null!;
    private Mock<ISessionExpiry> _expiry = null!;
    private AccessTokenStore _tokens = null!;
    private TokenRefresher _refresher = null!;

    [SetUp]
    public void SetUp()
    {
        _auth = new Mock<IAuthGateway>();
        _session = new Mock<ISessionStore>();
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh-1");
        _session.Setup(s => s.SaveRefreshTokenAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _session.Setup(s => s.ClearAsync()).Returns(Task.CompletedTask);
        _expiry = new Mock<ISessionExpiry>();
        _tokens = new AccessTokenStore { AccessToken = "access-1" };
        _refresher = new TokenRefresher(_auth.Object, _session.Object, _tokens, _expiry.Object);
    }

    private void GatewayReturns(Result<SessionTokens> result) =>
        _auth.Setup(a => a.RefreshAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(result);

    // Brak zapisanego refresh tokenu → false bez wywołania bramy
    [Test]
    public async Task Refresh_NoRefreshToken_ReturnsFalseWithoutGateway()
    {
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync((string?)null);

        var refreshed = await _refresher.RefreshAsync(CancellationToken.None);

        Assert.That(refreshed.IsSuccess, Is.False);
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Sukces → nowy access token w AccessTokenStore, true
    [Test]
    public async Task Refresh_Success_SetsNewAccessToken()
    {
        GatewayReturns(Result<SessionTokens>.Success(NewTokens));

        var refreshed = await _refresher.RefreshAsync(CancellationToken.None);

        Assert.That(refreshed.IsSuccess, Is.True);
        Assert.That(_tokens.AccessToken, Is.EqualTo("access-2"));
    }

    // Sukces → nowy refresh token zapisany (rotacja – stary jest jednorazowy)
    [Test]
    public async Task Refresh_Success_SavesRotatedRefreshToken()
    {
        GatewayReturns(Result<SessionTokens>.Success(NewTokens));

        await _refresher.RefreshAsync(CancellationToken.None);

        _session.Verify(s => s.SaveRefreshTokenAsync("refresh-2"), Times.Once);
        _session.Verify(s => s.ClearAsync(), Times.Never);
        _expiry.Verify(e => e.OnExpired(), Times.Never);
    }

    // Brama dostaje zapisany refresh token
    [Test]
    public async Task Refresh_PassesStoredRefreshTokenToGateway()
    {
        GatewayReturns(Result<SessionTokens>.Success(NewTokens));

        await _refresher.RefreshAsync(CancellationToken.None);

        _auth.Verify(a => a.RefreshAsync("refresh-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // Odmowa (Unauthorized) → token wyczyszczony, sesja wyczyszczona, OnExpired, false
    [Test]
    public async Task Refresh_Unauthorized_ClearsSessionAndNotifiesExpiry()
    {
        GatewayReturns(Result<SessionTokens>.Failure(new Error("Api.Unauthorized", ErrorType.Unauthorized)));

        var refreshed = await _refresher.RefreshAsync(CancellationToken.None);

        Assert.That(refreshed.IsSuccess, Is.False);
        Assert.That(_tokens.AccessToken, Is.Null);
        _session.Verify(s => s.ClearAsync(), Times.Once);
        _expiry.Verify(e => e.OnExpired(), Times.Once);
        _session.Verify(s => s.SaveRefreshTokenAsync(It.IsAny<string>()), Times.Never);
    }

    // Brak sieci → false, nic nie czyści (sesja wróci przy następnej próbie)
    [Test]
    public async Task Refresh_Network_ReturnsFalseAndKeepsSession()
    {
        GatewayReturns(Result<SessionTokens>.Failure(new Error("Api.Network", ErrorType.Network)));

        var refreshed = await _refresher.RefreshAsync(CancellationToken.None);

        Assert.That(refreshed.IsSuccess, Is.False);
        Assert.That(_tokens.AccessToken, Is.EqualTo("access-1"));
        _session.Verify(s => s.ClearAsync(), Times.Never);
        _session.Verify(s => s.SaveRefreshTokenAsync(It.IsAny<string>()), Times.Never);
        _expiry.Verify(e => e.OnExpired(), Times.Never);
    }

    // Dwa równoczesne wywołania z dwóch instancji (TokenRefresher jest Transient) → jedno wywołanie bramy
    [Test]
    public async Task Refresh_ConcurrentCalls_CallGatewayOnce()
    {
        var pending = new TaskCompletionSource<Result<SessionTokens>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _auth.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(pending.Task);

        var first = _refresher.RefreshAsync(CancellationToken.None);
        var other = new TokenRefresher(_auth.Object, _session.Object, _tokens, _expiry.Object);
        var second = other.RefreshAsync(CancellationToken.None);
        pending.SetResult(Result<SessionTokens>.Success(NewTokens));
        var results = await Task.WhenAll(first, second);

        Assert.That(results.Select(r => r.IsSuccess), Is.EqualTo(new[] { true, true }));
        Assert.That(_tokens.AccessToken, Is.EqualTo("access-2"));
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Kolejne (nierównoczesne) odświeżenie po udanym – woła bramę ponownie
    [Test]
    public async Task Refresh_SequentialCalls_CallGatewayEachTime()
    {
        GatewayReturns(Result<SessionTokens>.Success(NewTokens));

        await _refresher.RefreshAsync(CancellationToken.None);
        await _refresher.RefreshAsync(CancellationToken.None);

        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
