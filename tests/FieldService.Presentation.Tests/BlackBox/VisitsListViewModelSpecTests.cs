using FieldService.Presentation.State;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits;
using static FieldService.Presentation.Tests.BlackBox.SpecVisitData;
using FieldService.Presentation.Tests.Visits;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>
/// ViewModel listy wizyt koordynuje: ładowanie w haku OnNavigatedToAsync, błąd → IUserFeedback,
/// odświeżanie, nawigacja do szczegółów przez INavigationService.
/// Przypadek użycia jest prawdziwy – atrapą jest tylko brama i zegar.
/// </summary>
[TestFixture]
public class VisitsListViewModelSpecTests
{
    private const string SummaryText = "Zakończone: 0 z 2 · do zrobienia: 2";
    private const string UpdatedText = "Aktualizacja: 10:00";

    private static readonly DateTimeOffset Now = At(12, 10);

    private Mock<IVisitsGateway> _gateway = null!;
    private Mock<IVisitItemFactory> _items = null!;
    private Mock<IVisitFormatter> _formatter = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<INavigationService> _navigation = null!;
    private VisitsListViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IVisitsGateway>();
        _items = new Mock<IVisitItemFactory>();
        _items.Setup(f => f.Create(It.IsAny<VisitSummary>())).Returns((VisitSummary s) => Item(s));
        _formatter = new Mock<IVisitFormatter>();
        _formatter.Setup(f => f.FormatDaySummary(It.IsAny<IReadOnlyList<VisitSummary>>())).Returns(SummaryText);
        _formatter.Setup(f => f.FormatUpdatedNow()).Returns(UpdatedText);
        _feedback = new Mock<IUserFeedback>();
        _feedback.Setup(f => f.ShowErrorAsync(It.IsAny<Error>())).Returns(Task.CompletedTask);
        _navigation = new Mock<INavigationService>();
        _navigation.Setup(n => n.NavigateAsync(It.IsAny<VisitDetailsRoute>())).ReturnsAsync(NavigationResult.Success);

        var handler = new GetTodayVisitsHandler(_gateway.Object, EmptyReplica(), Mock.Of<IVisitListSnapshotStore>(), Mock.Of<IClock>(c => c.Now == Now));
        _vm = new VisitsListViewModel(handler, _items.Object, _formatter.Object, _feedback.Object, _navigation.Object,
            new AppState(Mock.Of<IPreferencesStore>()), Mock.Of<ISyncScheduler>(),
            VisitsListTestDeps.Logger());
    }

    // 07: wizyty zmienione w telefonie – tu żadnych
    private static IVisitReplica EmptyReplica() =>
        Mock.Of<IVisitReplica>(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>())
            == Task.FromResult<IReadOnlyDictionary<Guid, VisitStatus>>(new Dictionary<Guid, VisitStatus>()));

    private void GatewayReturns(params VisitSummary[] visits) =>
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success(visits));

    private Error GatewayFails()
    {
        var error = new Error("Network.Unavailable", ErrorType.Network);
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(error));
        return error;
    }

    // --- Ładowanie ---

    [Test]
    public void PrzedWejsciemNaEkran_NicNieLaduje()
    {
        GatewayReturns(NewVisit(At(12, 11)));

        Assert.That(_vm.Visits, Is.Empty);
        _gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task OnNavigatedTo_LadujeElementyZFabrykiWKolejnosciPrzypadkuUzycia()
    {
        var b = NewVisit(At(12, 12), customer: "B");
        var a = NewVisit(At(12, 8), VisitStatus.Completed, customer: "A");
        GatewayReturns(b, a);

        await _vm.OnNavigatedToAsync(new VisitsListRoute());

        Assert.That(_vm.Visits.Select(v => v.CustomerName), Is.EqualTo(new[] { "A", "B" }));
        _items.Verify(f => f.Create(It.IsAny<VisitSummary>()), Times.Exactly(2));
    }

    [Test]
    public async Task OnNavigatedTo_UstawiaPodsumowanieICzasAktualizacjiZFormattera()
    {
        GatewayReturns(NewVisit(At(12, 11)), NewVisit(At(12, 13)));

        await _vm.OnNavigatedToAsync(new VisitsListRoute());

        Assert.That(_vm.Summary, Is.EqualTo(SummaryText));
        Assert.That(_vm.LastUpdated, Is.EqualTo(UpdatedText));
    }

    [Test]
    public async Task OnNavigatedTo_Blad_PokazujeBladPrzezFeedback()
    {
        var error = GatewayFails();

        await _vm.OnNavigatedToAsync(new VisitsListRoute());

        _feedback.Verify(f => f.ShowErrorAsync(error), Times.Once);
        Assert.That(_vm.Visits, Is.Empty);
        Assert.That(_vm.IsBusy, Is.False);
    }

    [Test]
    public async Task OnNavigatedTo_Sukces_NiePokazujeBledu()
    {
        GatewayReturns(NewVisit(At(12, 11)));

        await _vm.OnNavigatedToAsync(new VisitsListRoute());

        _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
    }

    [Test]
    public async Task PonowneWejscie_TaSamaKolekcja_ZawartoscZastapiona()
    {
        GatewayReturns(NewVisit(At(12, 11), customer: "A"), NewVisit(At(12, 12), customer: "B"));
        var collection = _vm.Visits;
        await _vm.OnNavigatedToAsync(new VisitsListRoute());

        GatewayReturns(NewVisit(At(12, 14), customer: "C"));
        await _vm.OnNavigatedToAsync(new VisitsListRoute());

        Assert.That(_vm.Visits, Is.SameAs(collection));
        Assert.That(_vm.Visits.Select(v => v.CustomerName), Is.EqualTo(new[] { "C" }));
    }

    [Test]
    public async Task PonowneWejscie_LadujePonownie()
    {
        GatewayReturns(NewVisit(At(12, 11)));

        await _vm.OnNavigatedToAsync(new VisitsListRoute());
        await _vm.OnNavigatedToAsync(new VisitsListRoute());

        _gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // --- Odświeżanie ---

    [Test]
    public async Task Refresh_LadujeDaneIZerujeIsRefreshing()
    {
        GatewayReturns(NewVisit(At(12, 11), customer: "A"));
        _vm.IsRefreshing = true; // tak ustawia RefreshView po pociągnięciu listy

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.IsRefreshing, Is.False);
        Assert.That(_vm.Visits.Select(v => v.CustomerName), Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public async Task Refresh_Blad_ZerujeIsRefreshingIPokazujeBlad()
    {
        var error = GatewayFails();
        _vm.IsRefreshing = true;

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.IsRefreshing, Is.False);
        _feedback.Verify(f => f.ShowErrorAsync(error), Times.Once);
    }

    [Test]
    public async Task Refresh_ZachowujeTeSamaKolekcje()
    {
        GatewayReturns(NewVisit(At(12, 11)));
        var collection = _vm.Visits;
        await _vm.OnNavigatedToAsync(new VisitsListRoute());

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.Visits, Is.SameAs(collection));
        Assert.That(_vm.Visits, Has.Count.EqualTo(1));
    }

    // --- Nawigacja ---

    [Test]
    public async Task OpenVisit_NawigujeDoSzczegolowZIdWizyty()
    {
        var item = Item(NewVisit(At(12, 11)));

        await _vm.OpenVisitCommand.ExecuteAsync(item);

        _navigation.Verify(n => n.NavigateAsync(new VisitDetailsRoute(new VisitId(item.Id))), Times.Once);
    }

    [Test]
    public async Task OpenVisit_NieLadujeDanychPonownie()
    {
        var item = Item(NewVisit(At(12, 11)));

        await _vm.OpenVisitCommand.ExecuteAsync(item);

        _gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
