using FieldService.Application.Abstractions;
using FieldService.Presentation.Common;
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
        services.AddSingleton<ILocalizer, MauiLocalizer>();
        services.AddSingleton<IPreferencesStore, MauiPreferencesStore>();
        return services;
    }

    public static IServiceCollection AddPages(this IServiceCollection services)
    {
        services.AddTransient<LoginPage>();
        services.AddTransient<VisitsListPage>();
        return services;
    }
}
