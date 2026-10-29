using FieldService.Application.Abstractions;
using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.UI.Navigation;
using FieldService.UI.Platform;
using FieldService.UI.Session.Pages;
using FieldService.UI.Visits.Pages;

namespace FieldService.UI;

// Rejestracje pierścienia UI: adaptery platformy (porty z Application i Presentation) i strony
public static class DependencyInjection
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IDialogService, MauiDialogService>();
        // Jeden lokalizator pod dwoma typami: port dla ViewModeli i klasa dla App (odświeżenie języka w OnResume)
        services.AddSingleton<MauiLocalizer>();
        services.AddSingleton<ILocalizer>(provider => provider.GetRequiredService<MauiLocalizer>());
        services.AddSingleton<IPreferencesStore, MauiPreferencesStore>();
        // Jedna usługa nawigacji pod dwoma typami: port dla ViewModeli i klasa dla App (strona startowa)
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(provider => provider.GetRequiredService<NavigationService>());
        services.AddSingleton<IAppPaths, MauiAppPaths>();
        services.AddSingleton<IPermissionService, MauiPermissionService>();
        services.AddSingleton<IPhotoCapture, MauiPhotoCapture>();
        return services;
    }

    public static IServiceCollection AddPages(this IServiceCollection services)
    {
        services.AddTransient<LoginPage>();
        services.AddTransient<VisitsListPage>();
        services.AddTransient<VisitDetailsPage>();
        return services;
    }
}
