using CommunityToolkit.Mvvm.ComponentModel;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Visits;

// Szczegóły wizyty z elementu listy (parametr nawigacji). Pełny ekran z akcjami – Blok 07.
public partial class VisitDetailsViewModel : BaseViewModel
{
    [ObservableProperty]
    public partial VisitItem? Visit { get; set; }

    public override Task OnNavigatedToAsync(object? parameter)
    {
        Visit = parameter as VisitItem;
        return Task.CompletedTask;
    }
}
