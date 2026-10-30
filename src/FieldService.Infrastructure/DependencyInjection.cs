using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Api;
using FieldService.Infrastructure.Auth;
using FieldService.Infrastructure.Caching;
using FieldService.Infrastructure.Http;
using FieldService.Infrastructure.Inventory;
using FieldService.Infrastructure.Orders;
using FieldService.Infrastructure.Outbox;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Visits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace FieldService.Infrastructure;

// Rejestracje adapterów (porty z Application → implementacje tutaj)
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, ApiOptions options,
        CachingOptions caching)
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

        // skrzynka nadawcza: harmonogram żyje z aplikacją, procesor (z bramą HTTP) powstaje na każdy przebieg
        services.AddTransient<OutboxProcessor>();
        services.AddSingleton<Func<OutboxProcessor>>(sp => sp.GetRequiredService<OutboxProcessor>);
        services.AddSingleton<ISyncScheduler, SyncScheduler>();

        services.AddCaching(caching);
        services.AddApiClients(options, caching);
        return services;
    }

    // Katalog części (na razie bez cache), czyszczenie cache technika, liczniki żądań do pomiarów
    public static IServiceCollection AddCaching(this IServiceCollection services, CachingOptions caching)
    {
        services.AddSingleton(caching);
        services.AddTransient<IPartsCatalog, PartsCatalog>();
        services.AddSingleton<ILocalCaches, LocalCaches>();
        services.AddSingleton<RequestStatistics>();
        services.AddSingleton<IRequestStatistics>(sp => sp.GetRequiredService<RequestStatistics>());
        return services;
    }

    // Klient API generowany z OpenAPI (FieldServiceApiClient) z potokiem handlerów: token, [licznik żądań],
    // korelacja, na końcu odporność (Polly). Bramy są Transient (typed client) – singletony biorą je przez Func<…>.
    public static IServiceCollection AddApiClients(this IServiceCollection services, ApiOptions options,
        CachingOptions caching)
    {
        services.AddSingleton<ApiExecutor>();
        services.AddTransient<AuthHeaderHandler>();
        services.AddTransient<RequestCounterHandler>();
        services.AddTransient<CorrelationIdHandler>();

        var api = services.AddHttpClient<FieldServiceApiClient>(http => ConfigureApi(http, options))
            .AddHttpMessageHandler<AuthHeaderHandler>();      // 1. token (401 → odświeżenie i jedno ponowienie)
        if (caching.CountRequests) api.AddHttpMessageHandler<RequestCounterHandler>(); // pomiar: to, co idzie do serwera
        api.AddHttpMessageHandler<CorrelationIdHandler>()
            .AddStandardResilienceHandler(ConfigureResilience); // 2. Polly: retry, limity czasu, bezpiecznik

        // logowanie i odświeżenie tokenu: bez AuthHeaderHandler (odświeżenie nie może wywołać odświeżenia)
        services.AddHttpClient(AuthClient, http => ConfigureApi(http, options))
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddStandardResilienceHandler(ConfigureResilience);
        services.AddTransient<IAuthGateway>(sp => new ApiAuthGateway(
            new FieldServiceApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(AuthClient)),
            sp.GetRequiredService<ApiExecutor>()));

        // jedna brama wizyt: odczyt (IVisitsGateway) i wysyłka zmian z outbox (IOutboxSender)
        services.AddTransient<ApiVisitsGateway>();
        services.AddTransient<IVisitsGateway>(sp => sp.GetRequiredService<ApiVisitsGateway>());
        services.AddTransient<IOutboxSender>(sp => sp.GetRequiredService<ApiVisitsGateway>());
        services.AddTransient<IInventoryGateway, ApiInventoryGateway>();
        services.AddTransient<IWorkOrdersGateway, ApiWorkOrdersGateway>();
        return services;
    }

    private const string AuthClient = "auth";

    // BaseAddress z „/” na końcu – klient składa ścieżki względne (api/visits/today). Bez „/” każde żądanie dałoby 404
    private static void ConfigureApi(HttpClient http, ApiOptions options)
    {
        http.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
        http.Timeout = TimeSpan.FromSeconds(60);
    }

    // Automatyczny retry tylko dla bezpiecznych metod (GET/HEAD/OPTIONS…): zapis po zgubionej odpowiedzi jest
    // niejednoznaczny – dlatego Idempotency-Key, a ponowienie zapisu = kolejna synchronizacja (outbox) albo technik
    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.DisableForUnsafeHttpMethods();
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);          // zdjęcia i listy po słabym LTE
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(40); // ≥ 2 × AttemptTimeout (walidacja opcji)
    }
}
