using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Session;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>
/// Logowanie: komenda dostępna, gdy e-mail zawiera „@” i PIN ma 4 znaki; sukces → zapis e-maila
/// (klucz "last_email"), wyczyszczony PIN i reset stosu do listy wizyt; błąd → ErrorMessage.
/// Przypadek użycia jest prawdziwy – atrapą jest brama logowania.
/// </summary>
[TestFixture]
public class LoginViewModelSpecTests
{
    private const string Email = "jan@firma.pl";
    private const string Pin = "1234";
    private const string InvalidCredentialsText = "Nieprawidłowy e-mail lub PIN.";

    private Mock<IAuthGateway> _auth = null!;
    private Mock<IPreferencesStore> _preferences = null!;
    private Mock<IErrorMessageProvider> _messages = null!;
    private Mock<INavigationService> _navigation = null!;
    private LoginViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _auth = new Mock<IAuthGateway>();
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("token-123"));
        _preferences = new Mock<IPreferencesStore>();
        _messages = new Mock<IErrorMessageProvider>();
        _messages.Setup(m => m.GetMessage(It.IsAny<Error>())).Returns(InvalidCredentialsText);
        _navigation = new Mock<INavigationService>();
        _navigation.Setup(n => n.ResetToAsync(It.IsAny<VisitsListRoute>())).ReturnsAsync(NavigationResult.Success);

        _vm = new LoginViewModel(new SignInHandler(_auth.Object, Mock.Of<IPreferencesStore>(), Mock.Of<ILocalTechnicianData>()), _preferences.Object, _messages.Object, _navigation.Object,
            new StartupNavigator(Mock.Of<IFlowDraftStore>(), Mock.Of<IUserFeedback>(), _navigation.Object, Mock.Of<ISyncScheduler>()));
    }

    private void Fill(string email, string pin)
    {
        _vm.Email = email;
        _vm.Pin = pin;
    }

    // --- CanExecute ---

    [Test]
    public void PoprawnyEmailICzterocyfrowyPin_KomendaDostepna()
    {
        Fill(Email, Pin);

        Assert.That(_vm.SignInCommand.CanExecute(null), Is.True);
    }

    [TestCase("", "")]
    [TestCase("janfirma.pl", "1234")]
    [TestCase("jan@firma.pl", "")]
    [TestCase("jan@firma.pl", "123")]
    [TestCase("jan@firma.pl", "12345")]
    [TestCase("", "1234")]
    public void NiepoprawneDane_KomendaNiedostepna(string email, string pin)
    {
        Fill(email, pin);

        Assert.That(_vm.SignInCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void ZmianaEmaila_PowiadamiaOZmianieCanExecute()
    {
        _vm.Pin = Pin;
        var raised = 0;
        _vm.SignInCommand.CanExecuteChanged += (_, _) => raised++;

        _vm.Email = Email;

        Assert.That(raised, Is.GreaterThan(0));
    }

    [Test]
    public void ZmianaPinu_PowiadamiaOZmianieCanExecute()
    {
        _vm.Email = Email;
        var raised = 0;
        _vm.SignInCommand.CanExecuteChanged += (_, _) => raised++;

        _vm.Pin = Pin;

        Assert.That(raised, Is.GreaterThan(0));
    }

    // --- Sukces ---

    [Test]
    public async Task Sukces_ZapisujeEmailPodKluczemLastEmail()
    {
        Fill(Email, Pin);

        await _vm.SignInCommand.ExecuteAsync(null);

        _preferences.Verify(p => p.Set("last_email", Email), Times.Once);
    }

    [Test]
    public async Task Sukces_ResetujeStosDoListyWizyt()
    {
        Fill(Email, Pin);

        await _vm.SignInCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.ResetToAsync(new VisitsListRoute()), Times.Once);
    }

    [Test]
    public async Task Sukces_CzysciPinIBlad()
    {
        Fill(Email, Pin);

        await _vm.SignInCommand.ExecuteAsync(null);

        Assert.That(_vm.Pin, Is.Empty);
        Assert.That(_vm.ErrorMessage, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Sukces_PoWczesniejszymBledzie_KomunikatZnika()
    {
        _auth.SetupSequence(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(new Error("Auth.InvalidCredentials", ErrorType.Unauthorized)))
            .ReturnsAsync(Result<string>.Success("token-123"));
        Fill(Email, "0000");
        await _vm.SignInCommand.ExecuteAsync(null);

        Fill(Email, Pin);
        await _vm.SignInCommand.ExecuteAsync(null);

        Assert.That(_vm.ErrorMessage, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Logowanie_PrzekazujeEmailIPinDoBramy()
    {
        Fill("  " + Email + " ", Pin);

        await _vm.SignInCommand.ExecuteAsync(null);

        _auth.Verify(a => a.SignInAsync(Email, Pin, It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Błąd ---

    [Test]
    public async Task Blad_UstawiaErrorMessageZDostawcyKomunikatow()
    {
        var error = new Error("Auth.InvalidCredentials", ErrorType.Unauthorized);
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(error));
        Fill(Email, "0000");

        await _vm.SignInCommand.ExecuteAsync(null);

        Assert.That(_vm.ErrorMessage, Is.EqualTo(InvalidCredentialsText));
        _messages.Verify(m => m.GetMessage(error), Times.Once);
    }

    [Test]
    public async Task Blad_BezNawigacjiIBezZapisuEmaila()
    {
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(new Error("Network.Unavailable", ErrorType.Network)));
        Fill(Email, Pin);

        await _vm.SignInCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.ResetToAsync(It.IsAny<VisitsListRoute>()), Times.Never);
        _navigation.Verify(n => n.NavigateAsync(It.IsAny<VisitsListRoute>()), Times.Never);
        _preferences.Verify(p => p.Set(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
