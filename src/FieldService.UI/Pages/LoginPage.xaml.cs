using FieldService.UI.ViewModels;

namespace FieldService.UI.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        // Po zalogowaniu lista wizyt zastępuje logowanie (bez powrotu przyciskiem wstecz).
        // Usługa nawigacji dojdzie w Bloku 09.
        viewModel.SignedIn += (_, _) => Window!.Page = new NavigationPage(new VisitsPage());
    }
}
