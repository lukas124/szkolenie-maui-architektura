using FieldService.Presentation.Feedback;
using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Session;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Session;

// Deep link przy zimnym starcie: zapamiętana wizyta otwierana po zalogowaniu / przywróceniu sesji
[TestFixture]
public class StartupNavigatorDeepLinkTests
{
    private static readonly VisitId Visit = new(Guid.Parse("3a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d"));

    private Mock<INavigationService> _navigation = null!;
    private Mock<ISyncScheduler> _sync = null!;
    private List<string> _calls = null!;
    private StartupNavigator _startup = null!;

    [SetUp]
    public void SetUp()
    {
        _calls = [];
        _navigation = new Mock<INavigationService>();
        _navigation.Setup(n => n.NavigateAsync(It.IsAny<VisitDetailsRoute>()))
            .Callback(() => _calls.Add("details"));
        _sync = new Mock<ISyncScheduler>();
        _sync.Setup(s => s.RequestSync()).Callback(() => _calls.Add("sync"));

        // Brak szkicu kreatora (atrapa luźna) – bez pytania o szkic
        _startup = new StartupNavigator(new Mock<IFlowDraftStore>().Object, new Mock<IUserFeedback>().Object,
            _navigation.Object, _sync.Object);
    }

    // Zapamiętana wizyta → szczegóły tej wizyty po wznowieniu
    [Test]
    public async Task Resume_RememberedVisit_NavigatesToVisitDetails()
    {
        _startup.RememberVisit(Visit);

        await _startup.ResumeAsync();

        _navigation.Verify(n => n.NavigateAsync(new VisitDetailsRoute(Visit)), Times.Once);
    }

    // Kolejność: najpierw prośba o wysyłkę, potem szczegóły wizyty
    [Test]
    public async Task Resume_RememberedVisit_RequestsSyncBeforeNavigation()
    {
        _startup.RememberVisit(Visit);

        await _startup.ResumeAsync();

        Assert.That(_calls, Is.EqualTo(new[] { "sync", "details" }));
    }

    // Zapamiętana wizyta otwierana tylko raz
    [Test]
    public async Task Resume_Twice_NavigatesToVisitOnce()
    {
        _startup.RememberVisit(Visit);

        await _startup.ResumeAsync();
        await _startup.ResumeAsync();

        _navigation.Verify(n => n.NavigateAsync(It.IsAny<VisitDetailsRoute>()), Times.Once);
        _sync.Verify(s => s.RequestSync(), Times.Exactly(2));
    }

    // Bez zapamiętanej wizyty – brak przejścia do szczegółów
    [Test]
    public async Task Resume_NoRememberedVisit_DoesNotNavigateToDetails()
    {
        await _startup.ResumeAsync();

        _navigation.Verify(n => n.NavigateAsync(It.IsAny<VisitDetailsRoute>()), Times.Never);
        _sync.Verify(s => s.RequestSync(), Times.Once);
    }
}
