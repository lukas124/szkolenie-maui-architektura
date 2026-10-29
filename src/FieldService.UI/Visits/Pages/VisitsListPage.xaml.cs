using FieldService.Presentation.Visits;

namespace FieldService.UI.Visits.Pages;

public partial class VisitsListPage : ContentPage
{
    private readonly VisitsListViewModel _viewModel;

    // Strona i ViewModel przychodzą z kontenera (korzeń kompozycji: MauiProgram)
    public VisitsListPage(VisitsListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    // Nawigacja jeszcze w code-behind – usługa nawigacji dojdzie w checkpoincie 03-presentation
    private async void OnVisitTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: VisitItem item })
            await Navigation.PushAsync(new VisitDetailsPage(item));
    }
}
