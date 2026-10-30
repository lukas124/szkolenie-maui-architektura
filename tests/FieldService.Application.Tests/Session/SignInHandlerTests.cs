using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;

namespace FieldService.Application.Tests.Session;

// Logowanie na telefonie, który przechodzi między technikami: dane poprzedniego technika nie wyciekają i nie giną.
[TestFixture]
public class SignInHandlerTests
{
    private const string Jan = "jan.kowalski@fieldservice.pl";
    private const string Anna = "anna.nowak@fieldservice.pl";
    private const string OwnerKey = "local_data_owner";

    private Mock<IAuthGateway> _auth = null!;
    private Mock<ILocalTechnicianData> _localData = null!;
    private Preferences _preferences = null!;
    private SignInHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _auth = new Mock<IAuthGateway>();
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Success(new SessionTokens("access-token", "refresh-token", "Technik")));
        _localData = new Mock<ILocalTechnicianData>();
        _preferences = new Preferences();
        _handler = new SignInHandler(_auth.Object, _preferences, _localData.Object, new AccessTokenStore(), Mock.Of<ISessionStore>());
    }

    [Test]
    public async Task FirstSignIn_RemembersOwnerAndDoesNotClear()
    {
        var result = await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_preferences.Get(OwnerKey), Is.EqualTo(Jan));
            _localData.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task SameTechnicianInDifferentCase_KeepsData()
    {
        await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);

        await _handler.HandleAsync(new SignIn(Jan.ToUpperInvariant(), "1234"), CancellationToken.None);

        _localData.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task OtherTechnicianWithoutUnsentChanges_ClearsDataOfPrevious()
    {
        await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);

        var result = await _handler.HandleAsync(new SignIn(Anna, "1234"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_preferences.Get(OwnerKey), Is.EqualTo(Anna));
            _localData.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    // Niewysłane zmiany Jana: Anna się nie zaloguje, serwer nie jest pytany, nic nie jest usuwane
    [Test]
    public async Task OtherTechnicianWithUnsentChanges_RefusesBeforeCallingServer()
    {
        await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);
        _localData.Setup(d => d.HasUnsentChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _auth.Invocations.Clear();

        var result = await _handler.HandleAsync(new SignIn(Anna, "1234"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(SessionErrors.UnsentChangesOfOtherTechnician));
            Assert.That(_preferences.Get(OwnerKey), Is.EqualTo(Jan));
            _auth.Verify(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _localData.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    // Nieudane logowanie (np. zły PIN) nie usuwa danych poprzedniego technika
    [Test]
    public async Task OtherTechnicianSignInFails_KeepsData()
    {
        await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Failure(new Error("Auth.InvalidCredentials", ErrorType.Unauthorized)));

        var result = await _handler.HandleAsync(new SignIn(Anna, "0000"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            _localData.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    // Po nieudanym logowaniu Anny dane nadal należą do Jana – jego kolejne logowanie niczego nie usuwa
    [Test]
    public async Task OtherTechnicianSignInFails_OwnerStaysAndPreviousSignsInWithoutClear()
    {
        await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);
        _auth.Setup(a => a.SignInAsync(Anna, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Failure(new Error("Auth.InvalidCredentials", ErrorType.Unauthorized)));
        await _handler.HandleAsync(new SignIn(Anna, "0000"), CancellationToken.None);

        var result = await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_preferences.Get(OwnerKey), Is.EqualTo(Jan));
            _localData.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    // Po odmowie (zmiany Jana) Jan loguje się normalnie i jego zmiany mogą pójść na serwer
    [Test]
    public async Task AfterRefusal_PreviousTechnicianSignsIn()
    {
        await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);
        _localData.Setup(d => d.HasUnsentChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await _handler.HandleAsync(new SignIn(Anna, "1234"), CancellationToken.None);

        var result = await _handler.HandleAsync(new SignIn(Jan, "1234"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            _localData.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    // Preferencje w pamięci (jak Preferences z MAUI)
    private sealed class Preferences : IPreferencesStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string? Get(string key) => _values.GetValueOrDefault(key);

        public void Set(string key, string value) => _values[key] = value;
    }
}
