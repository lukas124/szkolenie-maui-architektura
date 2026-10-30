using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Tests.Contract;
using FieldService.Presentation.Visits.Completion;
using FieldService.TestDoubles;

namespace FieldService.Presentation.Tests.Visits.Completion;

// Szkic kreatora, który przeżywa ubicie procesu: zapis po każdym przejściu kroku,
// powrót do zapisanego kroku, usunięcie po zakończeniu albo przerwaniu
[TestFixture]
public class CompleteVisitFlowDraftTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.FromHours(1));

    private Mock<IFlowDraftStore> _drafts = null!;
    private Mock<INavigationService> _navigation = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<IVisitReplica> _replica = null!;
    private Mock<IInventoryGateway> _inventory = null!;
    private Mock<IClock> _clock = null!;
    private SignatureStepViewModel _signature = null!;

    [SetUp]
    public void SetUp()
    {
        _drafts = new Mock<IFlowDraftStore>();
        _navigation = new Mock<INavigationService>();
        _feedback = new Mock<IUserFeedback>();
        _replica = new Mock<IVisitReplica>();
        _inventory = new Mock<IInventoryGateway>();
        _inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Ok<IReadOnlyList<CatalogPart>>(Array.Empty<CatalogPart>()));
        _inventory.Setup(i => i.GetVanStockAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Ok<IReadOnlyList<VanStockItem>>(Array.Empty<VanStockItem>()));
        _clock = new Mock<IClock>();
        _clock.Setup(c => c.Now).Returns(Now);
    }

    // Szczegóły wizyty z repliki: jedna czynność, żeby krok podpisu był poprawny
    private void ReplicaHasDetails(VisitId id) =>
        _replica.Setup(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Details(id, Now.AddHours(-1), new[] { "Diagnostyka" }));

    private CompleteVisitFlowViewModel CreateFlow(IVisitRepository repository)
    {
        var localizer = ContractTestData.KeyLocalizer().Object;
        var draft = new CompletionDraft();
        var details = new GetVisitDetailsHandler(_replica.Object, new Mock<IVisitsGateway>().Object, new Mock<IVisitPhotoStore>().Object);

        var activities = new ActivitiesStepViewModel(draft, new RecordActivityHandler(repository, _clock.Object), details, localizer, _feedback.Object);
        var parts = new PartsStepViewModel(draft, new UsePartHandler(repository, _clock.Object), new RemoveUsedPartHandler(repository, _clock.Object),
            new GetVanStockHandler(_inventory.Object), details, _feedback.Object, localizer, Mock.Of<INavigationService>());
        _signature = new SignatureStepViewModel(draft, new SignByCustomerHandler(repository, _clock.Object), details, _feedback.Object, localizer);
        var summary = new SummaryStepViewModel(draft, new GetVisitProtocolHandler(repository, _inventory.Object, _clock.Object), _feedback.Object, localizer);

        return new CompleteVisitFlowViewModel(draft, activities, parts, _signature, summary, details,
            new CompleteVisitHandler(repository, _clock.Object), _feedback.Object, _navigation.Object, localizer, _drafts.Object);
    }

    private async Task<(CompleteVisitFlowViewModel Flow, VisitId Id)> EnterWithMockRepositoryAsync()
    {
        var id = new VisitId(Guid.NewGuid());
        ReplicaHasDetails(id);
        var flow = CreateFlow(new Mock<IVisitRepository>().Object);
        await flow.OnNavigatedToAsync(new CompleteVisitRoute(id));
        return (flow, id);
    }

    [Test]
    public async Task NextCommand_FromFirstStep_SavesDraftWithNextStepIndex()
    {
        var (flow, id) = await EnterWithMockRepositoryAsync();

        await flow.NextCommand.ExecuteAsync(null);

        Assert.That(flow.CurrentIndex, Is.EqualTo(1));
        _drafts.Verify(d => d.SaveAsync(new FlowDraft(id, 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task NextCommand_TwoSteps_SavesDraftAfterEachStep()
    {
        var (flow, id) = await EnterWithMockRepositoryAsync();

        await flow.NextCommand.ExecuteAsync(null);
        await flow.NextCommand.ExecuteAsync(null);

        _drafts.Verify(d => d.SaveAsync(new FlowDraft(id, 1), It.IsAny<CancellationToken>()), Times.Once);
        _drafts.Verify(d => d.SaveAsync(new FlowDraft(id, 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task OnNavigatedToAsync_DraftForSameVisit_ReturnsToSavedStep()
    {
        var id = new VisitId(Guid.NewGuid());
        ReplicaHasDetails(id);
        _drafts.Setup(d => d.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FlowDraft(id, 2));
        var flow = CreateFlow(new Mock<IVisitRepository>().Object);

        await flow.OnNavigatedToAsync(new CompleteVisitRoute(id));

        Assert.Multiple(() =>
        {
            Assert.That(flow.CurrentIndex, Is.EqualTo(2));
            Assert.That(flow.CurrentStep, Is.SameAs(flow.Steps[2]));
        });
    }

    [Test]
    public async Task OnNavigatedToAsync_DraftForOtherVisit_StartsFromFirstStep()
    {
        var id = new VisitId(Guid.NewGuid());
        ReplicaHasDetails(id);
        _drafts.Setup(d => d.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlowDraft(new VisitId(Guid.NewGuid()), 2));
        var flow = CreateFlow(new Mock<IVisitRepository>().Object);

        await flow.OnNavigatedToAsync(new CompleteVisitRoute(id));

        Assert.That(flow.CurrentIndex, Is.EqualTo(0));
    }

    [Test]
    public async Task OnNavigatedToAsync_NoDraft_StartsFromFirstStep()
    {
        var (flow, _) = await EnterWithMockRepositoryAsync();

        Assert.That(flow.CurrentIndex, Is.EqualTo(0));
    }

    [Test]
    public async Task CancelCommand_Executed_ClearsDraftAndClosesModal()
    {
        var (flow, _) = await EnterWithMockRepositoryAsync();
        await flow.NextCommand.ExecuteAsync(null);

        await flow.CancelCommand.ExecuteAsync(null);

        _drafts.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
        _navigation.Verify(n => n.CloseModalAsync(), Times.Once);
    }

    [Test]
    public async Task NextCommand_OnLastStepAndCompleted_ClearsDraft()
    {
        // Wizyta w toku z jedną czynnością – podpis i zakończenie przechodzą przez prawdziwe przypadki użycia
        var (repository, id) = ArrangeInProgressVisit();
        var recorded = await new RecordActivityHandler(repository, _clock.Object)
            .HandleAsync(new RecordActivity(id, "DIAG", "Diagnostyka"), CancellationToken.None);
        Assert.That(recorded.IsSuccess, Is.True);
        ReplicaHasDetails(id);
        var flow = CreateFlow(repository);
        await flow.OnNavigatedToAsync(new CompleteVisitRoute(id));

        await flow.NextCommand.ExecuteAsync(null);   // czynności → części
        await flow.NextCommand.ExecuteAsync(null);   // części → podpis
        _signature.HasDrawing = true;                // klient narysował podpis
        await flow.NextCommand.ExecuteAsync(null);   // podpis → podsumowanie
        Assert.That(flow.CurrentIndex, Is.EqualTo(3));
        await flow.NextCommand.ExecuteAsync(null);   // „Zakończ wizytę”

        _drafts.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
        _navigation.Verify(n => n.CloseModalAsync(), Times.Once);
    }

    [Test]
    public async Task NextCommand_MiddleStep_DoesNotClearDraft()
    {
        var (flow, _) = await EnterWithMockRepositoryAsync();

        await flow.NextCommand.ExecuteAsync(null);

        _drafts.Verify(d => d.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // Jedyne miejsce zależne od API TestDoubles (kontrakt 08 – patrz PYTANIA.md)
    private (IVisitRepository Repository, VisitId Id) ArrangeInProgressVisit()
    {
        var visit = TestVisits.InProgress();
        var repository = new InMemoryVisitRepository();
        repository.Add(visit);
        return (repository, visit.Id);
    }
}
