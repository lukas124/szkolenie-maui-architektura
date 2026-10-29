using FieldService.UI.Session.Pages;
using FieldService.UI.Visits.Pages;

namespace FieldService.UI;

// Pełna nazwa klasy MAUI: w przestrzeni FieldService.* samo „Application” to nasz projekt FieldService.Application
public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    // App należy do korzenia kompozycji, więc może znać kontener (poza App sięgają do niego tylko
    // rozszerzenia napisów w XAML – {loc:Translate}, też w UI)
    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    // Start: logowanie; po zalogowaniu lista wizyt zastępuje logowanie (bez powrotu przyciskiem wstecz).
    // Okno może powstać więcej niż raz w czasie życia procesu (Android), dlatego strony zawsze z kontenera.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var login = _services.GetRequiredService<LoginPage>();
        var window = new Window(new NavigationPage(login));
        login.ViewModel.SignedIn += (_, _) =>
            window.Page = new NavigationPage(_services.GetRequiredService<VisitsListPage>());
        return window;
    }
}
