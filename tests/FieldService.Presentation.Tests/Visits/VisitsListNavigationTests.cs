using CommunityToolkit.Mvvm.Messaging;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.State;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

public class VisitsListNavigationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    private Mock<INavigationService> _navigation = null!;
    private VisitsListViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.Now).Returns(Now);
        var appState = new Mock<IAppState>();
        appState.Setup(s => s.Filter).Returns(new VisitFilterState());

        _navigation = new Mock<INavigationService>();
        _navigation.Setup(n => n.NavigateAsync(It.IsAny<VisitHistoryRoute>())).ReturnsAsync(NavigationResult.Success);
        _navigation.Setup(n => n.NavigateAsync(It.IsAny<VanStockRoute>())).ReturnsAsync(NavigationResult.Success);

        // Lista bez wejścia na ekran – sprawdzamy tylko nowe komendy nawigacji
        var handler = new GetTodayVisitsHandler(new Mock<IVisitsGateway>().Object, new Mock<IVisitReplica>().Object,
            new Mock<IVisitListSnapshotStore>().Object, clock.Object);
        _viewModel = new VisitsListViewModel(handler, new Mock<IVisitItemFactory>().Object, new Mock<IVisitFormatter>().Object,
            new Mock<IUserFeedback>().Object, _navigation.Object, appState.Object, new Mock<ISyncScheduler>().Object);
    }

    [TearDown]
    public void TearDown()
    {
        // ViewModel rejestruje się w komunikatorze w konstruktorze
        WeakReferenceMessenger.Default.UnregisterAll(_viewModel);
    }

    [Test]
    public async Task OpenHistoryCommand_Executed_NavigatesToVisitHistory()
    {
        await _viewModel.OpenHistoryCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateAsync(new VisitHistoryRoute()), Times.Once);
    }

    [Test]
    public async Task OpenVanStockCommand_Executed_NavigatesToVanStock()
    {
        await _viewModel.OpenVanStockCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateAsync(new VanStockRoute()), Times.Once);
    }

    [Test]
    public async Task OpenHistoryCommand_Executed_DoesNotNavigateToVanStock()
    {
        await _viewModel.OpenHistoryCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateAsync(It.IsAny<VanStockRoute>()), Times.Never);
    }

    [Test]
    public async Task OpenVanStockCommand_Executed_DoesNotNavigateToHistory()
    {
        await _viewModel.OpenVanStockCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateAsync(It.IsAny<VisitHistoryRoute>()), Times.Never);
    }
}
