using System.Globalization;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common; // ZAŁOŻENIE: Result<T> w tej przestrzeni nazw
using FieldService.Presentation.Common;
using FieldService.Presentation.Diagnostics;
using FieldService.Presentation.Tests.Visits;
using Moq;
using NUnit.Framework;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Tests.Diagnostics;

// Ekran Diagnostyka: teksty z liczników i cache, komendy pomiarowe
[TestFixture]
public sealed class DiagnosticsViewModelTests
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["Diagnostics_Requests"] = "Żądania: {0} · 200: {1} · 304: {2} · {3:0.0} KB",
        ["Diagnostics_Cache"] = "Wpisy cache: {0}",
        ["Diagnostics_LiveViewModels"] = "Żywe ViewModele: szczegóły {0}, kreator {1}",
    };

    private CultureInfo _previousCulture = null!;
    private Mock<IRequestStatistics> _stats = null!;
    private Mock<ILocalCaches> _caches = null!;
    private Mock<IPartsCatalog> _catalog = null!;
    private TodayVisitsFixture _today = null!;
    private Mock<ILocalizer> _localizer = null!;

    [SetUp]
    public void SetUp()
    {
        _previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
        _stats = new Mock<IRequestStatistics>();
        _stats.Setup(s => s.Snapshot()).Returns(new RequestStats(10, 1, 4, 12698)); // 12698 B = 12,4 KB
        _caches = new Mock<ILocalCaches>();
        _caches.Setup(c => c.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7);
        _catalog = new Mock<IPartsCatalog>();
        _catalog.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Success([new CatalogPart("P-FILTR-01", "Filtr siatkowy", 25m)]));
        _today = new TodayVisitsFixture();
        _today.GatewayReturns();
        _localizer = new Mock<ILocalizer>();
        _localizer.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => Texts.TryGetValue(key, out var text) ? text : key);
        _localizer.Setup(l => l.CurrentCulture).Returns(CultureInfo.GetCultureInfo("pl-PL"));
    }

    [TearDown]
    public void TearDown() => CultureInfo.CurrentCulture = _previousCulture;

    private DiagnosticsViewModel CreateViewModel() =>
        new(_stats.Object, _caches.Object, new GetPartCatalogHandler(_catalog.Object), _today.CreateHandler(),
            _localizer.Object);

    private async Task<DiagnosticsViewModel> CreateAndNavigateAsync()
    {
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(new DiagnosticsRoute());
        return viewModel;
    }

    // RequestsText z migawki statystyk: liczby i KB z jednym miejscem po przecinku
    [Test]
    public async Task OnNavigatedToAsync_WithStats_SetsRequestsText()
    {
        var viewModel = await CreateAndNavigateAsync();

        Assert.That(viewModel.RequestsText, Is.EqualTo("Żądania: 10 · 200: 1 · 304: 4 · 12,4 KB"));
    }

    // CacheText z liczby wpisów we wszystkich cache
    [Test]
    public async Task OnNavigatedToAsync_WithCaches_SetsCacheText()
    {
        var viewModel = await CreateAndNavigateAsync();

        Assert.That(viewModel.CacheText, Is.EqualTo("Wpisy cache: 7"));
    }

    // LiveViewModelsText – liczby żywych szczegółów i kreatora (wartości zależą od innych testów w procesie)
    [Test]
    public async Task OnNavigatedToAsync_Always_SetsLiveViewModelsText()
    {
        var viewModel = await CreateAndNavigateAsync();

        Assert.That(viewModel.LiveViewModelsText, Does.Match(@"^Żywe ViewModele: szczegóły \d+, kreator \d+$"));
    }

    // „10 × otwórz Części” → 10 zapytań o katalog, potem teksty odświeżone
    [Test]
    public async Task OpenPartsTenTimesCommand_Executed_QueriesCatalogTenTimes()
    {
        var viewModel = await CreateAndNavigateAsync();
        _stats.Setup(s => s.Snapshot()).Returns(new RequestStats(11, 2, 4, 2048));

        await viewModel.OpenPartsTenTimesCommand.ExecuteAsync(null);

        _catalog.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Exactly(10));
        Assert.That(viewModel.RequestsText, Is.EqualTo("Żądania: 11 · 200: 2 · 304: 4 · 2,0 KB"));
    }

    // „Odśwież listę 5×” → 5 zapytań GetTodayVisits (5 wywołań bramy), potem teksty odświeżone
    [Test]
    public async Task RefreshListFiveTimesCommand_Executed_QueriesTodayVisitsFiveTimes()
    {
        var viewModel = await CreateAndNavigateAsync();
        _caches.Setup(c => c.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(9);

        await viewModel.RefreshListFiveTimesCommand.ExecuteAsync(null);

        _today.Gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Exactly(5));
        Assert.That(viewModel.CacheText, Is.EqualTo("Wpisy cache: 9"));
    }

    // Reset → stats.Reset() i teksty odświeżone
    [Test]
    public async Task ResetCommand_Executed_ResetsStatisticsAndRefreshesTexts()
    {
        var viewModel = await CreateAndNavigateAsync();
        _stats.Setup(s => s.Reset())
            .Callback(() => _stats.Setup(s => s.Snapshot()).Returns(new RequestStats(0, 0, 0, 0)));

        await viewModel.ResetCommand.ExecuteAsync(null);

        _stats.Verify(s => s.Reset(), Times.Once);
        Assert.That(viewModel.RequestsText, Is.EqualTo("Żądania: 0 · 200: 0 · 304: 0 · 0,0 KB"));
    }

    // Wymuś GC → teksty odświeżone (nowa migawka statystyk i liczba wpisów)
    [Test]
    public async Task ForceGcCommand_Executed_RefreshesTexts()
    {
        var viewModel = await CreateAndNavigateAsync();
        _caches.Setup(c => c.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        await viewModel.ForceGcCommand.ExecuteAsync(null);

        Assert.That(viewModel.CacheText, Is.EqualTo("Wpisy cache: 0"));
    }

    // Odśwież → teksty z bieżących wartości
    [Test]
    public async Task RefreshCommand_Executed_RefreshesTexts()
    {
        var viewModel = await CreateAndNavigateAsync();
        _stats.Setup(s => s.Snapshot()).Returns(new RequestStats(3, 3, 0, 1024));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.That(viewModel.RequestsText, Is.EqualTo("Żądania: 3 · 200: 3 · 304: 0 · 1,0 KB"));
    }

    // Teksty zmieniają się z powiadomieniem (observable) – UI je odświeża
    [Test]
    public async Task RefreshCommand_Executed_RaisesPropertyChangedForRequestsText()
    {
        var viewModel = await CreateAndNavigateAsync();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _stats.Setup(s => s.Snapshot()).Returns(new RequestStats(3, 3, 0, 1024));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.That(changed, Does.Contain(nameof(DiagnosticsViewModel.RequestsText)));
    }
}
