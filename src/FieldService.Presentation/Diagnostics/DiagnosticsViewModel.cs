using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Visits.Queries;
using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits;
using FieldService.Presentation.Visits.Completion;

namespace FieldService.Presentation.Diagnostics;

// Diagnostyka (tylko Debug i Test): liczby do pomiarów „przed / po” – żądania HTTP, wpisy cache, żywe ViewModele –
// i przyciski, które powtarzają scenariusz pomiaru zawsze tak samo.
public sealed partial class DiagnosticsViewModel(
    IRequestStatistics stats,
    ILocalCaches caches,
    GetPartCatalogHandler catalog,
    GetTodayVisitsHandler today,
    ILocalizer localizer) : BaseViewModel, INavigatedTo<DiagnosticsRoute>
{
    [ObservableProperty]
    public partial string RequestsText { get; set; } = "";

    [ObservableProperty]
    public partial string CacheText { get; set; } = "";

    [ObservableProperty]
    public partial string LiveViewModelsText { get; set; } = "";

    public Task OnNavigatedToAsync(DiagnosticsRoute route) => RefreshAsync();

    // P1: 10 × katalog części – bez cache 10 żądań, z PartsCatalog jedno
    [RelayCommand]
    private async Task OpenPartsTenTimesAsync()
    {
        for (var i = 0; i < 10; i++) await catalog.HandleAsync(new GetPartCatalog(), CancellationToken.None);
        await RefreshAsync();
    }

    // P2: 5 × lista wizyt – bez ETagu 5 × 200, z ETagCacheHandler 5 × 304 (lista z ETagiem jest już zapisana po wejściu)
    [RelayCommand]
    private async Task RefreshListFiveTimesAsync()
    {
        for (var i = 0; i < 5; i++) await today.HandleAsync(new GetTodayVisits(), CancellationToken.None);
        await RefreshAsync();
    }

    [RelayCommand]
    private Task ResetAsync()
    {
        stats.Reset();
        return RefreshAsync();
    }

    // Wyciek: ViewModel osiągalny po pełnym GC to ViewModel, który coś trzyma
    [RelayCommand]
    private Task ForceGcAsync()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return RefreshAsync();
    }

    // Awaria do pokazu raportu: aplikacja się zamyka, raport idzie do backendu przy następnym starcie
    [RelayCommand]
    private void ForceCrash() => throw new InvalidOperationException("Wymuszona awaria (Diagnostyka)");

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var requests = stats.Snapshot();
        RequestsText = string.Format(localizer.CurrentCulture, localizer["Diagnostics_Requests"],
            requests.Total, requests.Ok, requests.NotModified, requests.Bytes / 1024.0);
        CacheText = string.Format(localizer.CurrentCulture, localizer["Diagnostics_Cache"], await caches.CountAsync(CancellationToken.None));
        LiveViewModelsText = string.Format(localizer.CurrentCulture, localizer["Diagnostics_LiveViewModels"],
            ViewModelTracker.CountAlive(typeof(VisitDetailsViewModel)), ViewModelTracker.CountAlive(typeof(CompleteVisitFlowViewModel)));
    }
}
