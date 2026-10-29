using FieldService.Presentation.Session;
using FieldService.UI.Navigation;
using FieldService.UI.Platform;

namespace FieldService.UI;

// Pełna nazwa klasy MAUI: w przestrzeni FieldService.* samo „Application” to nasz projekt FieldService.Application
public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    // App należy do korzenia kompozycji, więc może znać kontener. Poza App znają go tylko NavigationService
    // (fabryka stron) i rozszerzenia napisów w XAML ({loc:Translate}) – też w UI, części korzenia kompozycji
    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    // Jeden stos nawigacji (NavigationPage) na całe okno, bez Shella; start: logowanie.
    // Okno może powstać więcej niż raz w czasie życia procesu (Android), dlatego stronę startową za każdym razem
    // tworzy usługa nawigacji (strona i ViewModel z kontenera, BindingContext przypisany).
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(_services.GetRequiredService<NavigationService>().CreateStartPage<LoginViewModel>()));

    // Powrót do aplikacji: strefę czasową telefonu czytamy od nowa, bo technik mógł ją zmienić albo przejechać do innej
    protected override void OnResume()
    {
        base.OnResume();
        TimeZoneInfo.ClearCachedData();
        // język też: zmieniony w ustawieniach telefonu odświeża napisy stron bez ponownego uruchamiania
        _services.GetRequiredService<MauiLocalizer>().RefreshCulture();
    }
}
