using FieldService.Presentation.Session;
using FieldService.Presentation.Visits;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Presentation;

// Rejestracje ViewModeli (wywoływane z korzenia kompozycji – MauiProgram)
public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddTransient<LoginViewModel>();
        services.AddTransient<VisitsListViewModel>();
        return services;
    }
}
