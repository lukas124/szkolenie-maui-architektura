using FieldService.UI.Pages;

namespace FieldService.UI;

// Pełna nazwa klasy MAUI: w przestrzeni FieldService.* samo „Application” to nasz projekt FieldService.Application
public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    // App należy do korzenia kompozycji, więc może znać kontener (nigdzie indziej tego nie robimy)
    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    // Okno może powstać więcej niż raz w czasie życia procesu (Android), dlatego strona startowa
    // za każdym razem przychodzi z kontenera.
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(_services.GetRequiredService<LoginPage>()));
}
