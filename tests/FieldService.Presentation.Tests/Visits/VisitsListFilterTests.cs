using CommunityToolkit.Mvvm.Messaging;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Messages;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.State;
using FieldService.Presentation.Tests.Contract;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

// Lista wizyt z filtrem ze stanu aplikacji; FilterChangedMessage → ponowne ładowanie
[TestFixture]
public class VisitsListFilterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.FromHours(1));

    private VisitFilterState _filter = null!;
    private Mock<IAppState> _appState = null!;
    private Mock<IVisitsGateway> _gateway = null!;
    private Mock<IVisitReplica> _replica = null!;
    private Mock<IClock> _clock = null!;
    private Mock<INavigationService> _navigation = null!;
    private VisitsListViewModel? _viewModel;

    [SetUp]
    public void SetUp()
    {
        _filter = new VisitFilterState();
        _appState = new Mock<IAppState>();
        _appState.Setup(a => a.Filter).Returns(_filter);

        // Trzy wizyty: spóźniona, zaplanowana na później, w toku
        _gateway = new Mock<IVisitsGateway>();
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Ok<IReadOnlyList<VisitSummary>>(new[]
            {
                ContractTestData.Summary(Guid.NewGuid(), VisitStatus.Planned, Now.AddHours(-1)),
                ContractTestData.Summary(Guid.NewGuid(), VisitStatus.Planned, Now.AddHours(3)),
                ContractTestData.Summary(Guid.NewGuid(), VisitStatus.InProgress, Now.AddHours(-2)),
            }));

        _replica = new Mock<IVisitReplica>();
        _replica.Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VisitStatus>());

        _clock = new Mock<IClock>();
        _clock.Setup(c => c.Now).Returns(Now);

        _navigation = new Mock<INavigationService>();
    }

    [TearDown]
    public void TearDown()
    {
        // Posłaniec jest globalny – odbiorca nie może przejść do kolejnego testu
        if (_viewModel is not null)
        {
            WeakReferenceMessenger.Default.UnregisterAll(_viewModel);
        }
    }

    private VisitsListViewModel CreateViewModel()
    {
        var handler = new GetTodayVisitsHandler(_gateway.Object, _replica.Object, _clock.Object);
        _viewModel = new VisitsListViewModel(handler, new Mock<IVisitItemFactory>().Object, new Mock<IVisitFormatter>().Object,
            new Mock<IUserFeedback>().Object, _navigation.Object, _appState.Object);
        return _viewModel;
    }

    private async Task<VisitsListViewModel> CreateAndEnterAsync()
    {
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(new VisitsListRoute());
        return viewModel;
    }

    [Test]
    public async Task OnNavigatedToAsync_NoFilter_LoadsAllVisits()
    {
        var viewModel = await CreateAndEnterAsync();

        Assert.That(viewModel.Visits, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task OnNavigatedToAsync_OnlyOverdueInAppState_LoadsOnlyOverdueVisits()
    {
        _filter.OnlyOverdue = true;

        var viewModel = await CreateAndEnterAsync();

        Assert.That(viewModel.Visits, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OnNavigatedToAsync_StatusInAppState_LoadsOnlyThatStatus()
    {
        _filter.Status = VisitStatus.Planned;

        var viewModel = await CreateAndEnterAsync();

        Assert.That(viewModel.Visits, Has.Count.EqualTo(2));
    }

    [Test]
    [Ignore("Ćwiczenie 10: usuń Ignore po uzupełnieniu TODO")]
    public async Task FilterChanged_ReloadsWithFilter()
    {
        var viewModel = await CreateAndEnterAsync();
        Assert.That(viewModel.Visits, Has.Count.EqualTo(3));

        // Modal filtra zmienił stan aplikacji i wysłał komunikat
        _filter.OnlyOverdue = true;
        WeakReferenceMessenger.Default.Send(new FilterChangedMessage());
        await ContractTestData.WaitUntilAsync(() => viewModel.Visits.Count == 1);

        Assert.That(viewModel.Visits, Has.Count.EqualTo(1));
        _gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task FilterChanged_AfterUnregister_DoesNotReload()
    {
        // Odbiorca zarejestrowany w posłańcu globalnym – po wyrejestrowaniu nie reaguje
        var viewModel = await CreateAndEnterAsync();
        WeakReferenceMessenger.Default.UnregisterAll(viewModel);

        WeakReferenceMessenger.Default.Send(new FilterChangedMessage());

        _gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task OpenFilterCommand_Executed_OpensFilterModal()
    {
        var viewModel = CreateViewModel();

        await viewModel.OpenFilterCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.OpenModalAsync(It.IsAny<VisitFilterRoute>()), Times.Once);
    }

    [Test]
    public async Task OpenWorkOrdersCommand_Executed_NavigatesToWorkOrders()
    {
        var viewModel = CreateViewModel();

        await viewModel.OpenWorkOrdersCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateAsync(It.IsAny<WorkOrdersRoute>()), Times.Once);
    }

    [Test]
    public async Task IsFiltered_NoFilter_IsFalse()
    {
        var viewModel = await CreateAndEnterAsync();

        Assert.That(viewModel.IsFiltered, Is.False);
    }

    [Test]
    public async Task IsFiltered_OnlyOverdue_IsTrue()
    {
        _filter.OnlyOverdue = true;

        var viewModel = await CreateAndEnterAsync();

        Assert.That(viewModel.IsFiltered, Is.True);
    }

    [Test]
    public async Task IsFiltered_StatusOnly_IsTrue()
    {
        _filter.Status = VisitStatus.Completed;

        var viewModel = await CreateAndEnterAsync();

        Assert.That(viewModel.IsFiltered, Is.True);
    }

    [Test]
    public async Task IsFiltered_FilterClearedAndMessageSent_IsFalse()
    {
        _filter.OnlyOverdue = true;
        var viewModel = await CreateAndEnterAsync();

        _filter.OnlyOverdue = false;
        WeakReferenceMessenger.Default.Send(new FilterChangedMessage());
        await ContractTestData.WaitUntilAsync(() => !viewModel.IsFiltered);

        Assert.That(viewModel.IsFiltered, Is.False);
    }
}
