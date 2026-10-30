using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

public class VisitHistoryViewModelTests
{
    private const int TotalCount = 65;   // 30 + 30 + 5 – trzy strony
    private static readonly DateTimeOffset Planned = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly Error NetworkError = new("Visits.Network", ErrorType.Network);

    private Mock<IVisitsGateway> _gateway = null!;
    private Mock<IVisitItemFactory> _factory = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<INavigationService> _navigation = null!;
    private VisitHistoryViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IVisitsGateway>();
        _factory = new Mock<IVisitItemFactory>();
        _feedback = new Mock<IUserFeedback>();
        _navigation = new Mock<INavigationService>();

        // Atrapa fabryki: element z danych wpisu, OpenCommand woła przekazaną funkcję
        _factory.Setup(f => f.CreateHistory(It.IsAny<VisitHistoryEntry>(), It.IsAny<Func<Task>>()))
            .Returns((VisitHistoryEntry entry, Func<Task> open) => Item(entry, open));
        _feedback.Setup(f => f.ShowErrorAsync(It.IsAny<Error>())).Returns(Task.CompletedTask);
        _navigation.Setup(n => n.NavigateAsync(It.IsAny<VisitDetailsRoute>())).ReturnsAsync(NavigationResult.Success);

        _viewModel = new VisitHistoryViewModel(new GetVisitHistoryHandler(_gateway.Object), _factory.Object,
            _feedback.Object, _navigation.Object);
    }

    private static VisitHistoryItem Item(VisitHistoryEntry entry, Func<Task> open) => new()
    {
        Id = entry.Id,
        CustomerName = entry.CustomerName,
        DeviceName = entry.DeviceName,
        City = entry.City,
        WorkOrderNumber = entry.WorkOrderNumber,
        StatusText = "status",
        Tone = VisitTone.Success,
        WhenText = "wczoraj",
        ThumbnailUrl = entry.ThumbnailUrl,
        Thumbnail = _ => Task.FromResult(Stream.Null),
        PhotoUrl = entry.PhotoUrl,
        OpenCommand = new AsyncRelayCommand(open)
    };

    private static VisitHistoryEntry Entry(int number) =>
        new(Guid.NewGuid(), $"ZL-2610-{number:0000}", VisitStatus.Completed, Planned, Planned.AddHours(1),
            $"Klient {number}", "Piaseczno", "Pompa ciepła 8 kW", VisitOutcomeType.Fixed,
            $"https://api.test/api/images/{number}?size=thumb", $"https://api.test/api/images/{number}");

    // Strona historii: pełne strony po 30, ostatnia (3.) – 5 wpisów
    private static Result<VisitHistoryPageResult> Page(int page)
    {
        var count = page < 3 ? VisitHistoryViewModel.PageSize : TotalCount - 2 * VisitHistoryViewModel.PageSize;
        var first = (page - 1) * VisitHistoryViewModel.PageSize + 1;
        var items = Enumerable.Range(first, count).Select(Entry).ToList();
        return Result<VisitHistoryPageResult>.Success(new VisitHistoryPageResult(items, page, TotalCount));
    }

    private void SetupPages()
    {
        _gateway.Setup(g => g.GetHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int _, CancellationToken _) => Page(page));
    }

    private void VerifyPageQueried(int page, Times times) =>
        _gateway.Verify(g => g.GetHistoryAsync(page, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()), times);

    [Test]
    public async Task OnNavigatedToAsync_FirstEntry_QueriesFirstPage()
    {
        SetupPages();

        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        VerifyPageQueried(1, Times.Once());
        Assert.That(_viewModel.Visits, Has.Count.EqualTo(30));
        Assert.That(_viewModel.Visits[0].WorkOrderNumber, Is.EqualTo("ZL-2610-0001"));
    }

    [Test]
    public async Task OnNavigatedToAsync_FirstPage_AddsItemsWithSingleAddEvent()
    {
        SetupPages();
        var events = new List<NotifyCollectionChangedEventArgs>();
        _viewModel.Visits.CollectionChanged += (_, e) => events.Add(e);

        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
        Assert.That(events[0].NewItems, Has.Count.EqualTo(30));
    }

    [Test]
    public async Task OnNavigatedToAsync_FirstPage_CreatesItemsThroughFactory()
    {
        SetupPages();

        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        _factory.Verify(f => f.CreateHistory(It.IsAny<VisitHistoryEntry>(), It.IsAny<Func<Task>>()), Times.Exactly(30));
    }

    [Test]
    public async Task OnNavigatedToAsync_MoreThanOnePage_HasMoreIsTrue()
    {
        SetupPages();

        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        Assert.That(_viewModel.HasMore, Is.True);
    }

    [Test]
    public async Task OnNavigatedToAsync_AllOnFirstPage_HasMoreIsFalse()
    {
        var items = new[] { Entry(1), Entry(2) };
        _gateway.Setup(g => g.GetHistoryAsync(1, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Success(new VisitHistoryPageResult(items, 1, 2)));

        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        Assert.That(_viewModel.HasMore, Is.False);
    }

    [Test]
    public async Task LoadMore_AfterFirstPage_QueriesSecondPageAndAppendsItems()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        VerifyPageQueried(2, Times.Once());
        Assert.That(_viewModel.Visits, Has.Count.EqualTo(60));
        Assert.That(_viewModel.Visits[30].WorkOrderNumber, Is.EqualTo("ZL-2610-0031"));
    }

    [Test]
    public async Task LoadMore_SecondPage_AppendsWithSingleAddEventAtPreviousCount()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        var events = new List<NotifyCollectionChangedEventArgs>();
        _viewModel.Visits.CollectionChanged += (_, e) => events.Add(e);

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
        Assert.That(events[0].NewItems, Has.Count.EqualTo(30));
        Assert.That(events[0].NewStartingIndex, Is.EqualTo(30));
    }

    [Test]
    public async Task LoadMore_LastPageLoaded_HasMoreIsFalse()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);
        Assert.That(_viewModel.HasMore, Is.True);
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        VerifyPageQueried(3, Times.Once());
        Assert.That(_viewModel.Visits, Has.Count.EqualTo(TotalCount));
        Assert.That(_viewModel.HasMore, Is.False);
    }

    [Test]
    public async Task LoadMore_AllLoaded_DoesNotQueryAgain()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        _gateway.Verify(g => g.GetHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.That(_viewModel.Visits, Has.Count.EqualTo(TotalCount));
    }

    [Test]
    public async Task LoadMore_BeforeNavigatedTo_DoesNotQuery()
    {
        SetupPages();

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        _gateway.Verify(g => g.GetHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(_viewModel.Visits, Is.Empty);
    }

    [Test]
    public async Task LoadMore_WhileFirstPageLoading_DoesNotQueryAgain()
    {
        var firstPage = new TaskCompletionSource<Result<VisitHistoryPageResult>>();
        _gateway.Setup(g => g.GetHistoryAsync(1, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()))
            .Returns(firstPage.Task);
        var entering = _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);
        firstPage.SetResult(Page(1));
        await entering;

        _gateway.Verify(g => g.GetHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(_viewModel.Visits, Has.Count.EqualTo(30));
    }

    [Test]
    public async Task LoadMore_WhileNextPageLoading_DoesNotQueryAgain()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        var secondPage = new TaskCompletionSource<Result<VisitHistoryPageResult>>();
        _gateway.Setup(g => g.GetHistoryAsync(2, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()))
            .Returns(secondPage.Task);

        var first = _viewModel.LoadMoreCommand.ExecuteAsync(null);
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);   // równoległe wywołanie w trakcie ładowania
        secondPage.SetResult(Page(2));
        await first;

        VerifyPageQueried(2, Times.Once());
        VerifyPageQueried(3, Times.Never());
        Assert.That(_viewModel.Visits, Has.Count.EqualTo(60));
    }

    [Test]
    public async Task LoadMore_QueryFails_ShowsErrorAndKeepsVisits()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        _gateway.Setup(g => g.GetHistoryAsync(2, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Failure(NetworkError));

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        _feedback.Verify(f => f.ShowErrorAsync(NetworkError), Times.Once);
        Assert.That(_viewModel.Visits, Has.Count.EqualTo(30));
    }

    [Test]
    public async Task LoadMore_AfterFailedPage_RetriesSamePage()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        _gateway.SetupSequence(g => g.GetHistoryAsync(2, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Failure(NetworkError))
            .ReturnsAsync(Page(2));

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        VerifyPageQueried(2, Times.Exactly(2));
        VerifyPageQueried(3, Times.Never());
        Assert.That(_viewModel.Visits, Has.Count.EqualTo(60));
    }

    [Test]
    public async Task OnNavigatedToAsync_QueryFails_ShowsErrorAndVisitsStayEmpty()
    {
        _gateway.Setup(g => g.GetHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Failure(NetworkError));

        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());

        _feedback.Verify(f => f.ShowErrorAsync(NetworkError), Times.Once);
        Assert.That(_viewModel.Visits, Is.Empty);
    }

    [Test]
    public async Task OpenCommand_ItemExecuted_NavigatesToVisitDetails()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        var item = _viewModel.Visits[4];

        await item.OpenCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateAsync(new VisitDetailsRoute(new VisitId(item.Id))), Times.Once);
    }

    [Test]
    public async Task OnNavigatedToAsync_WhileLoading_IsBusyTrueThenFalse()
    {
        var firstPage = new TaskCompletionSource<Result<VisitHistoryPageResult>>();
        _gateway.Setup(g => g.GetHistoryAsync(1, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()))
            .Returns(firstPage.Task);

        var entering = _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        var busyDuringLoad = _viewModel.IsBusy;
        firstPage.SetResult(Page(1));
        await entering;

        Assert.That(busyDuringLoad, Is.True);
        Assert.That(_viewModel.IsBusy, Is.False);
    }

    [Test]
    public async Task LoadMore_WhileLoading_IsBusyTrueThenFalse()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        var secondPage = new TaskCompletionSource<Result<VisitHistoryPageResult>>();
        _gateway.Setup(g => g.GetHistoryAsync(2, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()))
            .Returns(secondPage.Task);

        var loading = _viewModel.LoadMoreCommand.ExecuteAsync(null);
        var busyDuringLoad = _viewModel.IsBusy;
        secondPage.SetResult(Page(2));
        await loading;

        Assert.That(busyDuringLoad, Is.True);
        Assert.That(_viewModel.IsBusy, Is.False);
    }

    [Test]
    public async Task LoadMore_QueryFails_IsBusyFalseAfterwards()
    {
        SetupPages();
        await _viewModel.OnNavigatedToAsync(new VisitHistoryRoute());
        _gateway.Setup(g => g.GetHistoryAsync(2, VisitHistoryViewModel.PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Failure(NetworkError));

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.That(_viewModel.IsBusy, Is.False);
    }
}
