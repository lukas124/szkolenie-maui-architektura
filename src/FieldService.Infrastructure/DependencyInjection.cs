using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Auth;
using FieldService.Infrastructure.Inventory;
using FieldService.Infrastructure.Orders;
using FieldService.Infrastructure.Outbox;
using FieldService.Infrastructure.Persistence;
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

        // lokalna replika wizyt w SQLite: ten sam obiekt jako repozytorium (zapis) i replika (odczyt szczegółów)
        services.AddSingleton<SqliteDb>();
        services.AddSingleton<SqliteVisitRepository>();
        services.AddSingleton<IVisitRepository>(sp => sp.GetRequiredService<SqliteVisitRepository>());
        services.AddSingleton<IVisitReplica>(sp => sp.GetRequiredService<SqliteVisitRepository>());
        services.AddSingleton<IVisitPhotoStore, SqliteVisitPhotoStore>();
        services.AddSingleton<IFlowDraftStore, SqliteFlowDraftStore>();
        services.AddSingleton<ILocalTechnicianData, SqliteTechnicianData>();
        services.AddSingleton<IVisitListSnapshotStore, SqliteVisitListSnapshotStore>();

        // jedna brama wizyt: odczyt (IVisitsGateway) i wysyłka zmian z outbox (IOutboxSender)
        services.AddHttpClient<HttpVisitsGateway>(http => Configure(http, options));
        services.AddTransient<IVisitsGateway>(sp => sp.GetRequiredService<HttpVisitsGateway>());
        services.AddTransient<IOutboxSender>(sp => sp.GetRequiredService<HttpVisitsGateway>());

        // skrzynka nadawcza: harmonogram żyje z aplikacją, procesor (z bramą HTTP) powstaje na każdy przebieg
        services.AddTransient<OutboxProcessor>();
        services.AddSingleton<Func<OutboxProcessor>>(sp => sp.GetRequiredService<OutboxProcessor>);
        services.AddSingleton<ISyncScheduler, SyncScheduler>();
        services.AddHttpClient<IAuthGateway, HttpAuthGateway>(http => Configure(http, options));
        services.AddHttpClient<IInventoryGateway, HttpInventoryGateway>(http => Configure(http, options));
        services.AddHttpClient<IWorkOrdersGateway, HttpWorkOrdersGateway>(http => Configure(http, options));
        return services;
    }

    private static void Configure(HttpClient http, ApiOptions options)
    {
        http.BaseAddress = new Uri(options.BaseUrl);
        http.Timeout = TimeSpan.FromSeconds(15);
    }
}
