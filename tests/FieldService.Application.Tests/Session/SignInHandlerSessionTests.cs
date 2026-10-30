using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Session;

// Zapis sesji po logowaniu (nowe w 13); reszta zachowania SignInHandler – testy z wcześniejszych checkpointów
[TestFixture]
public class SignInHandlerSessionTests
{
    private Mock<IAuthGateway> _auth = null!;
    private Mock<ISessionStore> _session = null!;
    private AccessTokenStore _tokens = null!;
    private SignInHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _auth = new Mock<IAuthGateway>();
        _session = new Mock<ISessionStore>();
        _session.Setup(s => s.SaveRefreshTokenAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _tokens = new AccessTokenStore();

        // Preferencje bez zapisanego właściciela danych i brak lokalnych danych innego technika (atrapy luźne)
        var preferences = new Mock<IPreferencesStore>();
        var localData = new Mock<ILocalTechnicianData>();

        _handler = new SignInHandler(_auth.Object, preferences.Object, localData.Object, _tokens, _session.Object);
    }

    // Udane logowanie → access token w pamięci, refresh token w bezpiecznym magazynie, wynik = imię technika
    [Test]
    public async Task Handle_Success_StoresTokensAndReturnsName()
    {
        _auth.Setup(a => a.SignInAsync("jan.kowalski@firma.pl", "1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Success(new SessionTokens("access-1", "refresh-1", "Jan Kowalski")));

        var result = await _handler.HandleAsync(new SignIn("jan.kowalski@firma.pl", "1234"), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("Jan Kowalski"));
        Assert.That(_tokens.AccessToken, Is.EqualTo("access-1"));
        _session.Verify(s => s.SaveRefreshTokenAsync("refresh-1"), Times.Once);
    }

    // Zły PIN → ten sam błąd, nic nie zapisane
    [Test]
    public async Task Handle_InvalidCredentials_StoresNothing()
    {
        var error = new Error("Auth.InvalidCredentials", ErrorType.Unauthorized);
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Failure(error));

        var result = await _handler.HandleAsync(new SignIn("jan.kowalski@firma.pl", "0000"), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(error));
        Assert.That(_tokens.AccessToken, Is.Null);
        _session.Verify(s => s.SaveRefreshTokenAsync(It.IsAny<string>()), Times.Never);
    }

    // Brak sieci → ten sam błąd, nic nie zapisane
    [Test]
    public async Task Handle_Network_StoresNothing()
    {
        var error = new Error("Api.Network", ErrorType.Network);
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Failure(error));

        var result = await _handler.HandleAsync(new SignIn("jan.kowalski@firma.pl", "1234"), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(error));
        Assert.That(_tokens.AccessToken, Is.Null);
        _session.Verify(s => s.SaveRefreshTokenAsync(It.IsAny<string>()), Times.Never);
    }
}
