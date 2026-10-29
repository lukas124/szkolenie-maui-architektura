using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Session;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Session;

// Po zalogowaniu: najpierw prośba o wysyłkę kolejki, potem (jak w 10) pytanie o niedokończony kreator
[TestFixture]
public class StartupNavigatorSyncTests
{
    private Mock<IFlowDraftStore> _drafts = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<INavigationService> _navigation = null!;
    private Mock<ISyncScheduler> _sync = null!;
    private List<string> _order = null!;

    [SetUp]
    public void SetUp()
    {
        _order = [];
        _drafts = new Mock<IFlowDraftStore>();
        _feedback = new Mock<IUserFeedback>();
        _navigation = new Mock<INavigationService>();
        _sync = new Mock<ISyncScheduler>();
        _sync.Setup(s => s.RequestSync()).Callback(() => _order.Add("RequestSync"));
        _feedback
            .Setup(f => f.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => _order.Add("Confirm"))
            .ReturnsAsync(false);
    }

    private StartupNavigator CreateNavigator() =>
        new(_drafts.Object, _feedback.Object, _navigation.Object, _sync.Object);

    private void DraftIs(FlowDraft? draft) =>
        _drafts.Setup(d => d.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(draft);

    [Test]
    public async Task ResumeAsync_NoDraft_CallsRequestSyncOnce()
    {
        DraftIs(null);

        await CreateNavigator().ResumeAsync();

        _sync.Verify(s => s.RequestSync(), Times.Once);
        _feedback.Verify(f => f.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task ResumeAsync_WithDraft_CallsRequestSyncBeforeAskingAboutDraft()
    {
        DraftIs(new FlowDraft(new VisitId(Guid.NewGuid()), 2));

        await CreateNavigator().ResumeAsync();

        Assert.That(_order, Is.EqualTo(new[] { "RequestSync", "Confirm" }));
    }

    [Test]
    public async Task ResumeAsync_WithDraftAndResume_OpensWizardAsIn10()
    {
        var visitId = new VisitId(Guid.NewGuid());
        DraftIs(new FlowDraft(visitId, 1));
        _feedback
            .Setup(f => f.ConfirmAsync("Startup_ResumeTitle", "Startup_ResumeMessage", "Startup_Resume", "Startup_Discard"))
            .ReturnsAsync(true);

        await CreateNavigator().ResumeAsync();

        _navigation.Verify(n => n.OpenModalAsync(new CompleteVisitRoute(visitId)), Times.Once);
        _sync.Verify(s => s.RequestSync(), Times.Once);
    }
}
