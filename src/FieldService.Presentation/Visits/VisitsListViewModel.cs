using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Visits.Queries;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Visits;

// Lista wizyt na dziś. ViewModel koordynuje: przypadek użycia → Result → fabryka → kolekcja albo komunikat błędu.
// Nie formatuje, nie buduje elementów, nie składa tekstów i nie zna stron.
public partial class VisitsListViewModel(
    GetTodayVisitsHandler getTodayVisits,
    IVisitItemFactory items,
    IVisitFormatter formatter,
    IUserFeedback feedback,
    INavigationService navigation) : BaseViewModel
{
    public ObservableCollection<VisitItem> Visits { get; } = [];

    // gest „pociągnij, aby odświeżyć” (RefreshView sam ustawia true)
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    public partial string LastUpdated { get; set; } = "";

    public override Task OnNavigatedToAsync(object? parameter) => LoadAsync(CancellationToken.None);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private Task OpenVisitAsync(VisitItem item) => navigation.NavigateToAsync<VisitDetailsViewModel>(item);

    private async Task LoadAsync(CancellationToken ct)
    {
        var result = await getTodayVisits.HandleAsync(new GetTodayVisits(), ct);
        if (!result.IsSuccess)
        {
            await feedback.ShowErrorAsync(result.Error!);
            return;
        }

        Visits.ReplaceWith(result.Value.Select(items.Create));
        Summary = formatter.FormatDaySummary(result.Value);
        LastUpdated = formatter.FormatUpdatedNow();
    }
}
