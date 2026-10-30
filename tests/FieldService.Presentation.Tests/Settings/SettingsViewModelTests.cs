using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Presentation.Common;
using FieldService.Presentation.Diagnostics;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Settings;

namespace FieldService.Presentation.Tests.Settings;

// Ustawienia (jak w aplikacji z podstaw): technik, środowisko, Diagnostyka, wylogowanie z potwierdzeniem
[TestFixture]
public class SettingsViewModelTests
{
    private AccessTokenStore _tokens = null!;
    private Mock<ISessionStore> _session = null!;
    private Mock<ILocalCaches> _caches = null!;
    private Mock<ILocalTechnicianData> _localData = null!;
    private Mock<IPreferencesStore> _preferences = null!;
    private Mock<IAppEnvironment> _environment = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<INavigationService> _navigation = null!;
    private SettingsViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _tokens = new AccessTokenStore { AccessToken = "access-token" };
        _session = new Mock<ISessionStore>();
        _caches = new Mock<ILocalCaches>();
        _localData = new Mock<ILocalTechnicianData>();
        _preferences = new Mock<IPreferencesStore>();
        _preferences.Setup(p => p.Get(SignInHandler.OwnerKey)).Returns("jan@firma.pl");
        _preferences.Setup(p => p.Get(SignInHandler.TechnicianNameKey)).Returns("Jan Kowalski");
        _environment = new Mock<IAppEnvironment>();
        _environment.Setup(e => e.EnvironmentName).Returns("TEST");
        _environment.Setup(e => e.VersionText).Returns("1.0.0 (1)");
        _environment.Setup(e => e.IsDiagnostics).Returns(true);
        _feedback = new Mock<IUserFeedback>();
        _navigation = new Mock<INavigationService>();
        _viewModel = new SettingsViewModel(new SignOutHandler(_tokens, _session.Object, _caches.Object), _localData.Object,
            _preferences.Object, _environment.Object, _feedback.Object, _navigation.Object);
    }

    private void ConfirmReturns(bool answer) =>
        _feedback.Setup(f => f.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(answer);

    // Wejście: technik zapisany przy logowaniu, środowisko i wersja z platformy
    [Test]
    public async Task OnNavigatedTo_Always_ShowsTechnicianAndEnvironment()
    {
        await _viewModel.OnNavigatedToAsync(new SettingsRoute());

        Assert.That(_viewModel.TechnicianName, Is.EqualTo("Jan Kowalski"));
        Assert.That(_viewModel.Email, Is.EqualTo("jan@firma.pl"));
        Assert.That(_viewModel.EnvironmentName, Is.EqualTo("TEST"));
        Assert.That(_viewModel.VersionText, Is.EqualTo("1.0.0 (1)"));
        Assert.That(_viewModel.IsDiagnostics, Is.True);
    }

    // Diagnostyka – osobna strona
    [Test]
    public async Task OpenDiagnostics_Always_NavigatesToDiagnostics()
    {
        await _viewModel.OpenDiagnosticsCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateAsync(It.IsAny<DiagnosticsRoute>()), Times.Once);
    }

    // Wyloguj zawsze pyta; bez niewysłanych zmian – zwykły komunikat
    [Test]
    public async Task SignOut_NoUnsentChanges_AsksWithPlainMessage()
    {
        _localData.Setup(d => d.HasUnsentChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        ConfirmReturns(true);

        await _viewModel.SignOutCommand.ExecuteAsync(null);

        _feedback.Verify(f => f.ConfirmAsync("Settings_SignOutTitle", "Settings_SignOutMessage"), Times.Once);
    }

    // Niewysłane zmiany → ostrzeżenie, że zostają w telefonie
    [Test]
    public async Task SignOut_UnsentChanges_AsksWithPendingMessage()
    {
        _localData.Setup(d => d.HasUnsentChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ConfirmReturns(true);

        await _viewModel.SignOutCommand.ExecuteAsync(null);

        _feedback.Verify(f => f.ConfirmAsync("Settings_SignOutTitle", "Settings_SignOutPendingMessage"), Times.Once);
    }

    // Potwierdzone → sesja i cache wyczyszczone, ekran logowania; dane wizyt zostają
    [Test]
    public async Task SignOut_Confirmed_ClearsSessionAndCachesAndShowsLogin()
    {
        ConfirmReturns(true);

        await _viewModel.SignOutCommand.ExecuteAsync(null);

        Assert.That(_tokens.AccessToken, Is.Null);
        _session.Verify(s => s.ClearAsync(), Times.Once);
        _caches.Verify(c => c.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
        _localData.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
        _navigation.Verify(n => n.ResetToAsync(It.IsAny<LoginRoute>()), Times.Once);
    }

    // Rezygnacja → nic się nie zmienia
    [Test]
    public async Task SignOut_Cancelled_DoesNothing()
    {
        ConfirmReturns(false);

        await _viewModel.SignOutCommand.ExecuteAsync(null);

        Assert.That(_tokens.AccessToken, Is.EqualTo("access-token"));
        _session.Verify(s => s.ClearAsync(), Times.Never);
        _caches.Verify(c => c.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
        _navigation.Verify(n => n.ResetToAsync(It.IsAny<LoginRoute>()), Times.Never);
    }
}
