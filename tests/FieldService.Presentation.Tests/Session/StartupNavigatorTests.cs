using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Session;

namespace FieldService.Presentation.Tests.Session;

// Start po zalogowaniu: niedokończony kreator → pytanie „Wróć do niedokończonej wizyty?”
[TestFixture]
public class StartupNavigatorTests
{
    private static readonly VisitId VisitId = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    private Mock<IFlowDraftStore> _drafts = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<INavigationService> _navigation = null!;

    [SetUp]
    public void SetUp()
    {
        _drafts = new Mock<IFlowDraftStore>();
        _feedback = new Mock<IUserFeedback>();
        _navigation = new Mock<INavigationService>();
    }

    private StartupNavigator CreateNavigator() => new(_drafts.Object, _feedback.Object, _navigation.Object);

    private void DraftExists() =>
        _drafts.Setup(d => d.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FlowDraft(VisitId, 2));

    private void UserAnswers(bool resume) =>
        _feedback.Setup(f => f.ConfirmAsync("Startup_ResumeTitle", "Startup_ResumeMessage", "Startup_Resume", "Startup_Discard"))
            .ReturnsAsync(resume);

    [Test]
    public async Task ResumeAsync_NoDraft_DoesNotAskOrNavigate()
    {
        _drafts.Setup(d => d.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((FlowDraft?)null);

        await CreateNavigator().ResumeAsync();

        _feedback.Verify(f => f.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _navigation.Verify(n => n.OpenModalAsync(It.IsAny<CompleteVisitRoute>()), Times.Never);
        _drafts.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ResumeAsync_DraftExists_AsksWithResumeKeys()
    {
        DraftExists();
        UserAnswers(false);

        await CreateNavigator().ResumeAsync();

        _feedback.Verify(f => f.ConfirmAsync("Startup_ResumeTitle", "Startup_ResumeMessage", "Startup_Resume", "Startup_Discard"), Times.Once);
    }

    [Test]
    public async Task ResumeAsync_DraftExistsAndUserResumes_OpensWizardForDraftVisit()
    {
        DraftExists();
        UserAnswers(true);

        await CreateNavigator().ResumeAsync();

        _navigation.Verify(n => n.OpenModalAsync(It.Is<CompleteVisitRoute>(r => r.Id == VisitId)), Times.Once);
        _drafts.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ResumeAsync_DraftExistsAndUserDiscards_ClearsDraftWithoutNavigation()
    {
        DraftExists();
        UserAnswers(false);

        await CreateNavigator().ResumeAsync();

        _drafts.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
        _navigation.Verify(n => n.OpenModalAsync(It.IsAny<CompleteVisitRoute>()), Times.Never);
    }
}
