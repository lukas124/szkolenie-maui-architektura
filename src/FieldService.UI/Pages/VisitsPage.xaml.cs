using FieldService.UI.Models;
using FieldService.UI.ViewModels;

namespace FieldService.UI.Pages;

public partial class VisitsPage : ContentPage
{
    private readonly VisitsViewModel _viewModel;

    public VisitsPage()
    {
        InitializeComponent();
        _viewModel = new VisitsViewModel();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadData();
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject { BindingContext: VisitItem item }) return;

        // anulowanej wizyty nie otwieramy
        if (item.StatusText == "Anulowana")
        {
            await DisplayAlertAsync("Wizyta anulowana", $"Wizyta u klienta {item.Title} została anulowana.", "OK");
            return;
        }

        await Navigation.PushAsync(new VisitDetailsPage(item.Id));
    }
}
