using FieldService.Presentation.Diagnostics;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Settings;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Orders;
using FieldService.Presentation.Parts;
using FieldService.Presentation.Session;
using FieldService.Presentation.State;
using FieldService.Presentation.Visits;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Presentation;

// Rejestracje pierścienia prezentacji: usługi prezentacji, mapa tras i moduły (wywoływane z korzenia kompozycji – MauiProgram)
public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<IErrorMessageProvider, ErrorMessageProvider>();
        services.AddSingleton<IUserFeedback, UserFeedback>();

        var routes = new RouteMap().Map<LoginRoute, LoginViewModel>();
        services.AddSingleton(routes);
        services.AddSingleton<IAppState, AppState>();
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<StartupNavigator>();

        // Moduły = modele z mapy kontekstów, które mają ekrany w aplikacji
        services.AddVisitsPresentation(routes);
        services.AddOrdersPresentation(routes);
        services.AddPartsPresentation(routes);

        // Ustawienia i Diagnostyka – trasa Diagnostyki zawsze w mapie, strona tylko w Debug i Test (rejestruje ją UI)
        services.AddTransient<SettingsViewModel>();
        routes.Map<SettingsRoute, SettingsViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        routes.Map<DiagnosticsRoute, DiagnosticsViewModel>();
        return services;
    }
}
