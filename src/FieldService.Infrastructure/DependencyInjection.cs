using FieldService.Application.Abstractions;
using FieldService.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Infrastructure;

// Rejestracje adapterów (porty z Application → implementacje tutaj)
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, ApiOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IClock, SystemClock>();

        // TODO: Ćwiczenie 02 – zarejestruj bramę wizyt: services.AddHttpClient<IVisitsGateway, HttpVisitsGateway>(...)
        services.AddHttpClient<IAuthGateway, HttpAuthGateway>(http => Configure(http, options));
        return services;
    }

    private static void Configure(HttpClient http, ApiOptions options)
    {
        http.BaseAddress = new Uri(options.BaseUrl);
        http.Timeout = TimeSpan.FromSeconds(15);
    }
}
