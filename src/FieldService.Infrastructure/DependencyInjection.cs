using FieldService.Application.Abstractions;
using FieldService.Infrastructure.Auth;
using FieldService.Infrastructure.Visits;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Infrastructure;

// Rejestracje adapterów (porty z Application → implementacje tutaj)
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, ApiOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IClock, SystemClock>();

        services.AddHttpClient<IVisitsGateway, HttpVisitsGateway>(http => Configure(http, options));
        services.AddHttpClient<IAuthGateway, HttpAuthGateway>(http => Configure(http, options));
        return services;
    }

    private static void Configure(HttpClient http, ApiOptions options)
    {
        http.BaseAddress = new Uri(options.BaseUrl);
        http.Timeout = TimeSpan.FromSeconds(15);
    }
}
