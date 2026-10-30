using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Session;

[TestFixture]
public sealed class SignOutHandlerTests
{
    private AccessTokenStore _tokens = null!;
    private Mock<ISessionStore> _session = null!;
    private Mock<ILocalCaches> _caches = null!;
    private SignOutHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _tokens = new AccessTokenStore { AccessToken = "access-token" };
        _session = new Mock<ISessionStore>();
        _caches = new Mock<ILocalCaches>();
        _handler = new SignOutHandler(_tokens, _session.Object, _caches.Object);
    }

    // Wylogowanie zeruje token dostępu w pamięci
    [Test]
    public async Task HandleAsync_SignedIn_ClearsAccessToken()
    {
        await _handler.HandleAsync(CancellationToken.None);

        Assert.That(_tokens.AccessToken, Is.Null);
    }

    // Wylogowanie usuwa refresh token z bezpiecznego magazynu
    [Test]
    public async Task HandleAsync_SignedIn_ClearsSessionStore()
    {
        await _handler.HandleAsync(CancellationToken.None);

        _session.Verify(s => s.ClearAsync(), Times.Once);
    }

    // Wylogowanie czyści lokalne cache (katalog, ETag, migawka, miniatury)
    [Test]
    public async Task HandleAsync_SignedIn_ClearsLocalCaches()
    {
        await _handler.HandleAsync(CancellationToken.None);

        _caches.Verify(c => c.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Wylogowanie nie zapisuje nowego refresh tokenu ani nie liczy wpisów cache
    [Test]
    public async Task HandleAsync_SignedIn_DoesNothingElseWithSessionAndCaches()
    {
        await _handler.HandleAsync(CancellationToken.None);

        _session.Verify(s => s.SaveRefreshTokenAsync(It.IsAny<string>()), Times.Never);
        _caches.Verify(c => c.CountAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
