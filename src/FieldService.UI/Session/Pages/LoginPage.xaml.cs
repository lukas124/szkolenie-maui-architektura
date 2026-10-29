using FieldService.Presentation.Session;

namespace FieldService.UI.Session.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
    }

    // App (korzeń kompozycji) reaguje na SignedIn i pokazuje listę wizyt
    public LoginViewModel ViewModel { get; }
}
