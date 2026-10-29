using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Messages;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.State;
using FieldService.Presentation.Visits;
using FieldService.TestDoubles;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Visits;

// Lista wizyt a kolejka wysyłki: licznik „Oczekuje na wysłanie”, „Wyślij teraz”, wynik przebiegu (SyncCompletedMessage)
[TestFixture]
public class VisitsListSyncTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(150);

    private Mock<IVisitsGateway> _gateway = null!;
    private Mock<IVisitReplica> _replica = null!;
    private Mock<IVisitItemFactory> _itemFactory = null!;
    private Mock<IVisitFormatter> _formatter = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<INavigationService> _navigation = null!;
    private Mock<IAppState> _appState = null!;
    private Mock<ISyncScheduler> _sync = null!;
    private VisitsListViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IVisitsGateway>();
        GatewayReturnsEmptyList();
        _replica = new Mock<IVisitReplica>();
        _replica
            .Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VisitStatus>());
        _itemFactory = new Mock<IVisitItemFactory>();
        _formatter = new Mock<IVisitFormatter>();
        _formatter.Setup(f => f.FormatPending(It.IsAny<int>())).Returns((int count) => $"Oczekuje ({count})");
        _feedback = new Mock<IUserFeedback>();
        _navigation = new Mock<INavigationService>();
        _appState = new Mock<IAppState>();
        _appState.Setup(a => a.Filter).Returns(new VisitFilterState());
        _sync = new Mock<ISyncScheduler>();
        PendingCountIs(0);

        var handler = new GetTodayVisitsHandler(_gateway.Object, _replica.Object, Mock.Of<IVisitListSnapshotStore>(), new FixedClock());
        _viewModel = new VisitsListViewModel(handler, _itemFactory.Object, _formatter.Object, _feedback.Object,
            _navigation.Object, _appState.Object, _sync.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // WeakReferenceMessenger.Default jest globalny – sprzątamy po każdym teście
        WeakReferenceMessenger.Default.UnregisterAll(_viewModel);
    }

    private void GatewayReturnsEmptyList() =>
        _gateway
            .Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success(Array.Empty<VisitSummary>()));

    private void GatewayReturnsNetworkError() =>
        _gateway
            .Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(new Error("Visits.Network", ErrorType.Network)));

    private void PendingCountIs(int count) =>
        _sync.Setup(s => s.GetPendingCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(count);

    private int LoadCount() =>
        _gateway.Invocations.Count(i => i.Method.Name == nameof(IVisitsGateway.GetTodayAsync));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();   // tylko limit oczekiwania
        while (!condition() && stopwatch.Elapsed < WaitLimit)
            await Task.Delay(10);
    }

    private static void Send(SyncResult result) =>
        WeakReferenceMessenger.Default.Send(new SyncCompletedMessage(result));

    [Test]
    public async Task OnNavigatedToAsync_PendingMessages_SetsPendingCountHasPendingAndPendingText()
    {
        PendingCountIs(3);

        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        Assert.That(_viewModel.PendingCount, Is.EqualTo(3));
        Assert.That(_viewModel.HasPending, Is.True);
        Assert.That(_viewModel.PendingText, Is.EqualTo("Oczekuje (3)"));
    }

    [Test]
    public async Task OnNavigatedToAsync_NoPending_HasPendingFalse()
    {
        PendingCountIs(0);

        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        Assert.That(_viewModel.PendingCount, Is.EqualTo(0));
        Assert.That(_viewModel.HasPending, Is.False);
    }

    [Test]
    public async Task OnNavigatedToAsync_NetworkErrorWhileLoading_SetsPendingCount()
    {
        GatewayReturnsNetworkError();
        PendingCountIs(2);

        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        Assert.That(_viewModel.PendingCount, Is.EqualTo(2));
        Assert.That(_viewModel.HasPending, Is.True);
    }

    [Test]
    public void SendNowCommand_Execute_CallsRequestSync()
    {
        _viewModel.SendNowCommand.Execute(null);

        _sync.Verify(s => s.RequestSync(), Times.Once);
    }

    [Test]
    public async Task ReceiveSyncCompleted_NoSentOrConflicts_SetsPendingCountFromResult()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        Send(new SyncResult(0, 4, [], []));
        await WaitUntilAsync(() => _viewModel.PendingCount == 4);

        Assert.That(_viewModel.PendingCount, Is.EqualTo(4));
        Assert.That(_viewModel.HasPending, Is.True);
        Assert.That(_viewModel.PendingText, Is.EqualTo("Oczekuje (4)"));
    }

    [Test]
    public async Task ReceiveSyncCompleted_NoSentOrConflicts_DoesNotReloadList()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());
        var loadsBefore = LoadCount();

        Send(new SyncResult(0, 1, [], []));
        await Task.Delay(QuietPeriod);

        Assert.That(LoadCount(), Is.EqualTo(loadsBefore));
    }

    [Test]
    public async Task ReceiveSyncCompleted_MessagesSent_ReloadsList()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());
        var loadsBefore = LoadCount();

        Send(new SyncResult(2, 0, [], []));
        await WaitUntilAsync(() => LoadCount() > loadsBefore);

        Assert.That(LoadCount(), Is.EqualTo(loadsBefore + 1));
    }

    [Test]
    public async Task ReceiveSyncCompleted_MessagesSent_PendingCountMatchesQueue()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());
        PendingCountIs(0);

        Send(new SyncResult(2, 0, [], []));
        await WaitUntilAsync(() => _viewModel.PendingCount == 0);

        Assert.That(_viewModel.PendingCount, Is.EqualTo(0));
        Assert.That(_viewModel.HasPending, Is.False);
    }

    [Test]
    public async Task ReceiveSyncCompleted_Conflict_ShowsSyncConflictError()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        Send(new SyncResult(0, 0, [new VisitId(Guid.NewGuid())], []));
        await WaitUntilAsync(() => _feedback.Invocations.Any(i => i.Method.Name == nameof(IUserFeedback.ShowErrorAsync)));

        _feedback.Verify(f => f.ShowErrorAsync(SyncErrors.Conflict), Times.Once);
    }

    [Test]
    public async Task ReceiveSyncCompleted_Rejected_ShowsSyncRejectedError()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        Send(new SyncResult(0, 0, [], [new VisitId(Guid.NewGuid())]));
        await WaitUntilAsync(() => _feedback.Invocations.Any(i => i.Method.Name == nameof(IUserFeedback.ShowErrorAsync)));

        _feedback.Verify(f => f.ShowErrorAsync(SyncErrors.Rejected), Times.Once);
    }

    [Test]
    public async Task ReceiveSyncCompleted_Conflict_ReloadsList()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());
        var loadsBefore = LoadCount();

        Send(new SyncResult(0, 0, [new VisitId(Guid.NewGuid())], []));
        await WaitUntilAsync(() => LoadCount() > loadsBefore);

        Assert.That(LoadCount(), Is.EqualTo(loadsBefore + 1));
    }

    [Test]
    public async Task ReceiveSyncCompleted_NoConflict_DoesNotShowError()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        Send(new SyncResult(3, 0, [], []));
        await Task.Delay(QuietPeriod);

        _feedback.Verify(f => f.ShowErrorAsync(SyncErrors.Conflict), Times.Never);
    }

    [Test]
    public async Task PendingCount_Changed_NotifiesHasPendingAndPendingText()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());
        var changed = new List<string?>();
        ((INotifyPropertyChanged)_viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Send(new SyncResult(0, 5, [], []));
        await WaitUntilAsync(() => changed.Contains(nameof(VisitsListViewModel.PendingText)));

        Assert.That(changed, Does.Contain(nameof(VisitsListViewModel.PendingCount)));
        Assert.That(changed, Does.Contain(nameof(VisitsListViewModel.HasPending)));
        Assert.That(changed, Does.Contain(nameof(VisitsListViewModel.PendingText)));
    }
}
