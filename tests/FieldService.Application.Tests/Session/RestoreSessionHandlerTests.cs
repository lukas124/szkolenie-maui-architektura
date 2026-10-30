using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Session;

[TestFixture]
public class RestoreSessionHandlerTests
{
    private Mock<IAuthGateway> _auth = null!;
    private Mock<ISessionStore> _session = null!;
    private AccessTokenStore _tokens = null!;
    private RestoreSessionHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _auth = new Mock<IAuthGateway>();
        _session = new Mock<ISessionStore>();
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh-1");
        _session.Setup(s => s.SaveRefreshTokenAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _session.Setup(s => s.ClearAsync()).Returns(Task.CompletedTask);
        _tokens = new AccessTokenStore();
        _handler = new RestoreSessionHandler(_auth.Object, _session.Object, _tokens);
    }

    private void GatewayReturns(Result<SessionTokens> result) =>
        _auth.Setup(a => a.RefreshAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(result);

    // Brak zapisanej sesji → false bez wywołania bramy
    [Test]
    public async Task Handle_NoRefreshToken_ReturnsFalseWithoutGateway()
    {
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync((string?)null);

        var restored = await _handler.HandleAsync(CancellationToken.None);

        Assert.That(restored, Is.False);
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Sesja już jest (np. drugie okno) → true bez odświeżania – drugie odświeżenie tym samym tokenem by się nie udało
    [Test]
    public async Task Handle_SessionAlreadyActive_ReturnsTrueWithoutGateway()
    {
        _tokens.AccessToken = "access-1";

        var restored = await _handler.HandleAsync(CancellationToken.None);

        Assert.That(restored, Is.True);
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Zapisany token i udane odświeżenie → sesja przywrócona
    [Test]
    public async Task Handle_RefreshSucceeds_ReturnsTrueAndSetsAccessToken()
    {
        GatewayReturns(Result<SessionTokens>.Success(new SessionTokens("access-2", "refresh-2", "Jan Kowalski")));

        var restored = await _handler.HandleAsync(CancellationToken.None);

        Assert.That(restored, Is.True);
        Assert.That(_tokens.AccessToken, Is.EqualTo("access-2"));
    }

    // Udane odświeżenie → nowy refresh token zapisany (rotacja)
    [Test]
    public async Task Handle_RefreshSucceeds_SavesRotatedRefreshToken()
    {
        GatewayReturns(Result<SessionTokens>.Success(new SessionTokens("access-2", "refresh-2", "Jan Kowalski")));

        await _handler.HandleAsync(CancellationToken.None);

        _session.Verify(s => s.SaveRefreshTokenAsync("refresh-2"), Times.Once);
        _session.Verify(s => s.ClearAsync(), Times.Never);
    }

    // Odmowa → sesja wyczyszczona, false (cichy start: bez komunikatu o wygaśnięciu)
    [Test]
    public async Task Handle_Unauthorized_ClearsSessionAndReturnsFalse()
    {
        GatewayReturns(Result<SessionTokens>.Failure(new Error("Api.Unauthorized", ErrorType.Unauthorized)));

        var restored = await _handler.HandleAsync(CancellationToken.None);

        Assert.That(restored, Is.False);
        Assert.That(_tokens.AccessToken, Is.Null);
        _session.Verify(s => s.ClearAsync(), Times.Once);
    }

    // Brak sieci przy starcie → sesja zostaje (praca offline), token odświeży się po powrocie sieci
    [Test]
    public async Task Handle_Network_ReturnsTrueAndKeepsSession()
    {
        GatewayReturns(Result<SessionTokens>.Failure(new Error("Api.Network", ErrorType.Network)));

        var restored = await _handler.HandleAsync(CancellationToken.None);

        Assert.That(restored, Is.True);
        Assert.That(_tokens.AccessToken, Is.Null);
        _session.Verify(s => s.ClearAsync(), Times.Never);
    }
}
