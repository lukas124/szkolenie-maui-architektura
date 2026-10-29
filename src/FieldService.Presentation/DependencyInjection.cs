using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Session;
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
        services.AddTransient<LoginViewModel>();

        services.AddVisitsPresentation(routes);
        return services;
    }
}
