using FieldService.Presentation.State;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

// ViewModel testujemy bez emulatora: przypadek użycia z atrapą bramy, porty prezentacji jako atrapy
[TestFixture]
public class VisitsListViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 40, 0, TimeSpan.FromHours(1));

    private readonly Mock<IVisitsGateway> _gateway = new();
    private readonly Mock<IUserFeedback> _feedback = new();
    private readonly Mock<INavigationService> _navigation = new();

    private VisitsListViewModel CreateViewModel()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.Now).Returns(Now);
        var localizer = new TestLocalizer();
        var formatter = new VisitFormatter(localizer, clock.Object);
        return new VisitsListViewModel(
            new GetTodayVisitsHandler(_gateway.Object, EmptyReplica(), Mock.Of<IVisitListSnapshotStore>(), clock.Object),
            new VisitItemFactory(formatter, Mock.Of<IThumbnailCache>()),
            formatter,
            _feedback.Object,
            _navigation.Object,
            new AppState(Mock.Of<IPreferencesStore>()),
            Mock.Of<ISyncScheduler>(),
            VisitsListTestDeps.Logger());
    }

    private static VisitSummary Visit(string customer, VisitStatus status) =>
        new(Guid.NewGuid(), "ZL-2611-1001", status, Now.AddHours(1), Now.AddHours(2), customer, "ul. Lipowa 12, Piaseczno",
            "Pompa ciepła 8 kW", "Nie grzeje", IsBrigade: false, ThumbnailUrl: "https://example.com/t.png", Version: 1);

    [Test]
    public async Task OnNavigatedToAsync_FillsVisitsAndSummary()
    {
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success([Visit("Anna", VisitStatus.Completed), Visit("Piotr", VisitStatus.Planned)]));
        var vm = CreateViewModel();

        await vm.OnNavigatedToAsync(new VisitsListRoute());

        Assert.Multiple(() =>
        {
            Assert.That(vm.Visits.Select(v => v.CustomerName), Is.EqualTo(new[] { "Anna", "Piotr" }));
            Assert.That(vm.Summary, Is.EqualTo("Zakończone: 1 z 2 · do zrobienia: 1"));
        });
    }

    [Test]
    public async Task OnNavigatedToAsync_Error_ShowsErrorThroughFeedback()
    {
        var error = new Error("Visits.Network", ErrorType.Network);
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(error));
        var vm = CreateViewModel();

        await vm.OnNavigatedToAsync(new VisitsListRoute());

        Assert.That(vm.Visits, Is.Empty);
        _feedback.Verify(f => f.ShowErrorAsync(error), Times.Once);
    }

    [Test]
    public async Task OpenVisitCommand_NavigatesToDetailsWithVisitId()
    {
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success([Visit("Anna", VisitStatus.Planned)]));
        var vm = CreateViewModel();
        await vm.OnNavigatedToAsync(new VisitsListRoute());
        var item = vm.Visits.Single();

        await vm.OpenVisitCommand.ExecuteAsync(item);

        _navigation.Verify(n => n.NavigateAsync(new VisitDetailsRoute(new VisitId(item.Id))), Times.Once);
    }

    [Test]
    public async Task OnAppearingAsync_BeforeFirstLoad_DoesNotLoad_AfterReturnFromDetails_Reloads()
    {
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success([Visit("Anna", VisitStatus.Planned)]));
        var vm = CreateViewModel();

        await vm.OnAppearingAsync();
        await vm.OnNavigatedToAsync(new VisitsListRoute());
        await vm.OnAppearingAsync(); // np. po zamknięciu filtra – przeładowuje komunikat, nie pojawienie się
        await vm.OpenVisitCommand.ExecuteAsync(vm.Visits[0]);
        await vm.OnAppearingAsync(); // powrót ze szczegółów

        _gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static IVisitReplica EmptyReplica()
    {
        var replica = new Mock<IVisitReplica>();
        replica.Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VisitStatus>());
        return replica.Object;
    }
}
