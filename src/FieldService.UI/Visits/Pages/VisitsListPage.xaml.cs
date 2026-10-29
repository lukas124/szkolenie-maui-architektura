using FieldService.Presentation.Visits;
using FieldService.UI.Pages;

namespace FieldService.UI.Visits.Pages;

public partial class VisitsListPage : BasePage
{
    public VisitsListPage() => InitializeComponent();

    // TODO: Ćwiczenie 03 – nawigacja w code-behind: strona tworzy ViewModel i stronę szczegółów sama.
    // Przenieś do VisitsListViewModel (OpenVisitCommand + INavigationService) i usuń tę metodę.
    private async void OnVisitTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: VisitItem item })
        {
            var details = new VisitDetailsViewModel();
            await Navigation.PushAsync(new VisitDetailsPage { BindingContext = details });
            await details.OnNavigatedToAsync(item);
        }
    }
}
