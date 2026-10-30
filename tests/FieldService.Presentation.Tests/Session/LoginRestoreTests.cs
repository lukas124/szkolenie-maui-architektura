using FieldService.Presentation.Feedback;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Session;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Session;

// Zapamiętana sesja: pierwsze pojawienie się ekranu logowania próbuje ją przywrócić
[TestFixture]
public class LoginRestoreTests
{
    private Mock<IAuthGateway> _auth = null!;
    private Mock<ISessionStore> _session = null!;
    private Mock<INavigationService> _navigation = null!;
    private Mock<ISyncScheduler> _sync = null!;
    private AccessTokenStore _tokens = null!;
    private LoginViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _auth = new Mock<IAuthGateway>();
        _session = new Mock<ISessionStore>();
        _session.Setup(s => s.SaveRefreshTokenAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _session.Setup(s => s.ClearAsync()).Returns(Task.CompletedTask);
        _navigation = new Mock<INavigationService>();
        _sync = new Mock<ISyncScheduler>();
        _tokens = new AccessTokenStore();

        // Preferencje bez właściciela, brak szkicu (atrapy luźne)
        var preferences = new Mock<IPreferencesStore>();
        var signIn = new SignInHandler(_auth.Object, preferences.Object, new Mock<ILocalTechnicianData>().Object,
            _tokens, _session.Object);
        var startup = new StartupNavigator(new Mock<IFlowDraftStore>().Object, new Mock<IUserFeedback>().Object,
            _navigation.Object, _sync.Object);
        var restore = new RestoreSessionHandler(_auth.Object, _session.Object, _tokens);

        _viewModel = new LoginViewModel(signIn, preferences.Object, new Mock<IErrorMessageProvider>().Object,
            _navigation.Object, startup, restore);
    }

    private void SavedSessionRefreshes()
    {
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh-1");
        _auth.Setup(a => a.RefreshAsync("refresh-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Success(new SessionTokens("access-2", "refresh-2", "Jan Kowalski")));
    }

    // Sesja przywrócona → od razu lista wizyt i wznowienie (wysyłka outboxu)
    [Test]
    public async Task FirstAppearing_SessionRestored_ResetsToVisitsListAndResumes()
    {
        SavedSessionRefreshes();

        await _viewModel.OnAppearingAsync();

        _navigation.Verify(n => n.ResetToAsync(It.IsAny<VisitsListRoute>()), Times.Once);
        _sync.Verify(s => s.RequestSync(), Times.Once);
        Assert.That(_tokens.AccessToken, Is.EqualTo("access-2"));
    }

    // W trakcie przywracania logowanie jest zablokowane – przywrócona sesja nie nadpisze nowej
    [Test]
    public async Task FirstAppearing_RestoreInProgress_SignInDisabled()
    {
        var pending = new TaskCompletionSource<Result<SessionTokens>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh-1");
        _auth.Setup(a => a.RefreshAsync("refresh-1", It.IsAny<CancellationToken>())).Returns(pending.Task);
        _viewModel.Email = "jan@firma.pl";
        _viewModel.Pin = "1234";

        var appearing = _viewModel.OnAppearingAsync();
        var duringRestore = _viewModel.SignInCommand.CanExecute(null);
        pending.SetResult(Result<SessionTokens>.Failure(new Error("Api.Unauthorized", ErrorType.Unauthorized)));
        await appearing;

        Assert.That(duringRestore, Is.False);
        Assert.That(_viewModel.SignInCommand.CanExecute(null), Is.True);
    }

    // Brak zapisanej sesji → zostaje ekran logowania
    [Test]
    public async Task FirstAppearing_NoSession_StaysOnLogin()
    {
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync((string?)null);

        await _viewModel.OnAppearingAsync();

        _navigation.Verify(n => n.ResetToAsync(It.IsAny<VisitsListRoute>()), Times.Never);
        _sync.Verify(s => s.RequestSync(), Times.Never);
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Sesja odrzucona przez serwer → zostaje ekran logowania
    [Test]
    public async Task FirstAppearing_SessionRejected_StaysOnLogin()
    {
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh-1");
        _auth.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SessionTokens>.Failure(new Error("Api.Unauthorized", ErrorType.Unauthorized)));

        await _viewModel.OnAppearingAsync();

        _navigation.Verify(n => n.ResetToAsync(It.IsAny<VisitsListRoute>()), Times.Never);
        _sync.Verify(s => s.RequestSync(), Times.Never);
    }

    // Drugie pojawienie się ekranu (np. po wylogowaniu) – bez ponownej próby przywrócenia
    [Test]
    public async Task SecondAppearing_DoesNothing()
    {
        SavedSessionRefreshes();

        await _viewModel.OnAppearingAsync();
        await _viewModel.OnAppearingAsync();

        _session.Verify(s => s.GetRefreshTokenAsync(), Times.Once);
        _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _navigation.Verify(n => n.ResetToAsync(It.IsAny<VisitsListRoute>()), Times.Once);
    }

    // Drugie pojawienie się po nieudanym przywróceniu – też bez ponownej próby
    [Test]
    public async Task SecondAppearing_AfterNoSession_DoesNotRetry()
    {
        _session.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync((string?)null);

        await _viewModel.OnAppearingAsync();
        await _viewModel.OnAppearingAsync();

        _session.Verify(s => s.GetRefreshTokenAsync(), Times.Once);
    }
}
