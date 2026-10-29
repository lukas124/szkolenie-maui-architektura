using FieldService.Application.Session;
using FieldService.Application.Visits.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Application;

// Rejestracje pierścienia przypadków użycia (wywoływane z korzenia kompozycji – MauiProgram)
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<AccessTokenStore>();
        services.AddTransient<GetTodayVisitsHandler>();
        services.AddTransient<SignInHandler>();
        return services;
    }
}
