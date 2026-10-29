using FieldService.Presentation.Feedback;
using FieldService.Presentation.Session;
using FieldService.Presentation.Visits;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Presentation;

// Rejestracje pierścienia prezentacji: ViewModele i usługi prezentacji (wywoływane z korzenia kompozycji – MauiProgram)
public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<IErrorMessageProvider, ErrorMessageProvider>();
        services.AddSingleton<IUserFeedback, UserFeedback>();
        services.AddSingleton<IVisitFormatter, VisitFormatter>();
        services.AddSingleton<IVisitItemFactory, VisitItemFactory>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<VisitsListViewModel>();
        services.AddTransient<VisitDetailsViewModel>();
        return services;
    }
}
