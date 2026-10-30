using FieldService.Application.Session;
using FieldService.Infrastructure.Telemetry;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Session;
using FieldService.UI.Extensions;
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

    // W tło: telemetria z buforów idzie od razu (system może ubić proces, zanim minie minuta eksportu metryk)
    protected override void OnSleep()
    {
        base.OnSleep();
        _services.FlushTelemetry();
    }

    // Deep link tą samą drogą co przycisk: adres → trasa (DeepLinkParser) → usługa nawigacji. Tylko po zalogowaniu.
    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        // Bez sesji (zimny start, ekran logowania) link czeka na zalogowanie albo przywrócenie sesji (StartupNavigator);
        // przy otwartym modalu (kreator, dialog) link ginie – strona trafiłaby pod modal
        if (_services.GetRequiredService<AccessTokenStore>().AccessToken is null)
        {
            if (DeepLinkParser.Parse(uri) is VisitDetailsRoute linked)
                _services.GetRequiredService<StartupNavigator>().RememberVisit(linked.Id);
            return;
        }
        if (Windows.FirstOrDefault()?.Page?.Navigation.ModalStack.Count > 0) return;

        // Trasa z adresu ma typ IRoute – usługa nawigacji woła hak INavigatedTo<TRoute> po typie statycznym, więc
        // przekazujemy konkretny typ trasy (jedno miejsce, wpis na każdy rodzaj linku)
        var navigation = _services.GetRequiredService<INavigationService>();
        var opening = DeepLinkParser.Parse(uri) switch
        {
            VisitDetailsRoute details => navigation.NavigateAsync(details),
            VisitsListRoute list => navigation.ResetToAsync(list),
            _ => Task.FromResult(NavigationResult.NoRoute)
        };
        opening.FireAndForget();
    }

    // Jeden stos nawigacji (NavigationPage) na całe okno, bez Shella; start: logowanie.
    // Okno może powstać więcej niż raz w czasie życia procesu (Android), dlatego stronę startową za każdym razem
    // tworzy usługa nawigacji (strona i ViewModel z kontenera, BindingContext przypisany).
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var startPage = _services.GetRequiredService<NavigationService>().CreateStartPage<LoginRoute>();
        var window = new Window(new NavigationPage(startPage));
        _services.GetRequiredService<SyncTriggers>().Attach(window);
        return window;
    }

    // Powrót do aplikacji: strefę czasową telefonu czytamy od nowa, bo technik mógł ją zmienić albo przejechać do innej
    protected override void OnResume()
    {
        base.OnResume();
        TimeZoneInfo.ClearCachedData();
        // język też: zmieniony w ustawieniach telefonu odświeża napisy stron bez ponownego uruchamiania
        _services.GetRequiredService<MauiLocalizer>().RefreshCulture();
    }
}
