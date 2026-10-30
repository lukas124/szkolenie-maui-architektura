using CommunityToolkit.Mvvm.Messaging;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common; // ZAŁOŻENIE: Error, ErrorType w tej przestrzeni nazw
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using FieldService.Presentation.State;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Tests.Visits;

// Lista wizyt: stale-while-revalidate (migawka, potem sieć), teksty LastUpdated, wylogowanie
[TestFixture]
public sealed class VisitsListViewModelCacheTests
{
    private const string SnapshotText = "Dane z 7:42";
    private const string UpdatedNowText = "Zaktualizowano teraz";
    private const string OfflineText = "Offline od 7:42";

    private static readonly Guid FirstId = Guid.Parse("7a1c0e7b-0000-4c5a-8f5e-000000000001");
    private static readonly Guid SecondId = Guid.Parse("7a1c0e7b-0000-4c5a-8f5e-000000000002");
    private static readonly Error Network = new("Api.Network", ErrorType.Network);

    private TodayVisitsFixture _today = null!;
    private Mock<IVisitItemFactory> _items = null!;
    private Mock<IVisitFormatter> _formatter = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<INavigationService> _navigation = null!;
    private Mock<IAppState> _appState = null!;
    private Mock<ISyncScheduler> _sync = null!;

    [SetUp]
    public void SetUp()
    {
        _today = new TodayVisitsFixture();
        _items = new Mock<IVisitItemFactory>();
        _formatter = new Mock<IVisitFormatter>();
        _formatter.Setup(f => f.FormatSnapshot(It.IsAny<DateTimeOffset>())).Returns(SnapshotText);
        _formatter.Setup(f => f.FormatUpdatedNow()).Returns(UpdatedNowText);
        // ZAŁOŻENIE: IVisitFormatter.FormatOfflineSince(DateTimeOffset) z bloku 11
        _formatter.Setup(f => f.FormatOfflineSince(It.IsAny<DateTimeOffset>())).Returns(OfflineText);
        _feedback = new Mock<IUserFeedback>();
        _navigation = new Mock<INavigationService>();
        _appState = new Mock<IAppState>();
        _appState.Setup(a => a.Filter).Returns(new VisitFilterState());
        _sync = new Mock<ISyncScheduler>();
        _sync.Setup(s => s.GetPendingCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
    }

    [TearDown]
    public void TearDown() => WeakReferenceMessenger.Default.Reset();

    private VisitsListViewModel CreateViewModel() =>
        new(_today.CreateHandler(), _items.Object, _formatter.Object, _feedback.Object, _navigation.Object,
            _appState.Object, _sync.Object, NullLogger<VisitsListViewModel>.Instance);

    // ZAŁOŻENIE: wejście na listę = OnNavigatedToAsync(new VisitsListRoute())
    private static Task NavigateAsync(VisitsListViewModel viewModel) =>
        viewModel.OnNavigatedToAsync(new VisitsListRoute());

    private static List<string?> RecordLastUpdated(VisitsListViewModel viewModel)
    {
        var values = new List<string?>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VisitsListViewModel.LastUpdated))
            {
                values.Add(viewModel.LastUpdated);
            }
        };
        return values;
    }

    private void SnapshotWithOneVisit() =>
        _today.SnapshotContains(TodayVisitsFixture.SnapshotSavedAt,
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(2)));

    private void NetworkWithTwoVisits() =>
        _today.GatewayReturns(
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(2)),
            TodayVisitsFixture.Summary(SecondId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(4)));

    // SWR: najpierw migawka („Dane z 7:42”), potem sieć – na końcu lista z sieci i „Zaktualizowano teraz”
    [Test]
    public async Task OnNavigatedToAsync_SnapshotThenNetwork_ShowsSnapshotThenFreshList()
    {
        SnapshotWithOneVisit();
        NetworkWithTwoVisits();
        var viewModel = CreateViewModel();
        var lastUpdated = RecordLastUpdated(viewModel);

        await NavigateAsync(viewModel);

        Assert.That(lastUpdated, Does.Contain(SnapshotText));
        Assert.That(lastUpdated.IndexOf(SnapshotText), Is.LessThan(lastUpdated.LastIndexOf(UpdatedNowText)));
        Assert.That(viewModel.LastUpdated, Is.EqualTo(UpdatedNowText));
        Assert.That(viewModel.Visits, Has.Count.EqualTo(2));
        _formatter.Verify(f => f.FormatSnapshot(TodayVisitsFixture.SnapshotSavedAt), Times.Once);
        _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
    }

    // Elementy z migawki są tworzone, zanim przyjdzie odpowiedź z sieci
    [Test]
    public async Task OnNavigatedToAsync_WithSnapshot_CreatesItemsFromSnapshotFirst()
    {
        SnapshotWithOneVisit();
        NetworkWithTwoVisits();
        var viewModel = CreateViewModel();

        await NavigateAsync(viewModel);

        // 1 element z migawki + 2 z sieci
        _items.Verify(f => f.Create(It.IsAny<VisitSummary>()), Times.Exactly(3));
    }

    // Brak migawki: bez komunikatu błędu, lista z sieci
    [Test]
    public async Task OnNavigatedToAsync_NoSnapshot_ShowsNetworkListWithoutError()
    {
        NetworkWithTwoVisits();
        var viewModel = CreateViewModel();

        await NavigateAsync(viewModel);

        Assert.That(viewModel.Visits, Has.Count.EqualTo(2));
        Assert.That(viewModel.LastUpdated, Is.EqualTo(UpdatedNowText));
        _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
        _formatter.Verify(f => f.FormatSnapshot(It.IsAny<DateTimeOffset>()), Times.Never);
    }

    // Brak sieci, migawka jest: lista z migawki i LastUpdated = FormatOfflineSince(SnapshotAt), bez komunikatu
    [Test]
    public async Task OnNavigatedToAsync_NoNetworkWithSnapshot_ShowsOfflineSince()
    {
        SnapshotWithOneVisit();
        _today.GatewayFails(Network);
        var viewModel = CreateViewModel();

        await NavigateAsync(viewModel);

        Assert.That(viewModel.Visits, Has.Count.EqualTo(1));
        Assert.That(viewModel.LastUpdated, Is.EqualTo(OfflineText));
        _formatter.Verify(f => f.FormatOfflineSince(TodayVisitsFixture.SnapshotSavedAt), Times.Once);
        _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
    }

    // Brak sieci i brak migawki → komunikat błędu jak w 13, lista pusta
    [Test]
    public async Task OnNavigatedToAsync_NoNetworkNoSnapshot_ShowsError()
    {
        _today.GatewayFails(Network);
        var viewModel = CreateViewModel();

        await NavigateAsync(viewModel);

        Assert.That(viewModel.Visits, Is.Empty);
        _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Once);
    }

    // Migawka pokazana, a sieć zwraca błąd inny niż brak sieci → komunikat, lista z migawki zostaje
    [Test]
    public async Task OnNavigatedToAsync_SnapshotThenServerError_KeepsSnapshotListAndShowsError()
    {
        var unexpected = new Error("Api.Unexpected", ErrorType.Unexpected);
        SnapshotWithOneVisit();
        _today.GatewayFails(unexpected);
        var viewModel = CreateViewModel();

        await NavigateAsync(viewModel);

        Assert.That(viewModel.Visits, Has.Count.EqualTo(1));
        _feedback.Verify(f => f.ShowErrorAsync(unexpected), Times.Once);
    }

    // Lista niepusta (kolejne wejście) → bez kroku migawki, od razu sieć
    [Test]
    public async Task OnNavigatedToAsync_ListAlreadyFilled_SkipsSnapshotStep()
    {
        SnapshotWithOneVisit();
        NetworkWithTwoVisits();
        var viewModel = CreateViewModel();
        await NavigateAsync(viewModel);

        await NavigateAsync(viewModel);

        _formatter.Verify(f => f.FormatSnapshot(It.IsAny<DateTimeOffset>()), Times.Once);
        Assert.That(viewModel.LastUpdated, Is.EqualTo(UpdatedNowText));
    }
}
