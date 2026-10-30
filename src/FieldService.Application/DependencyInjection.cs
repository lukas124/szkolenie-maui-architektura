using FieldService.Application.Orders.Queries;
using FieldService.Application.Parts.Commands;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Session;
using FieldService.Application.Visits.Commands;
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
        services.AddTransient<GetVisitDetailsHandler>();
        services.AddTransient<GetVisitHistoryHandler>();
        services.AddTransient<StartVisitHandler>();
        services.AddTransient<RecordActivityHandler>();
        services.AddTransient<UsePartHandler>();
        services.AddTransient<RemoveUsedPartHandler>();
        services.AddTransient<SignByCustomerHandler>();
        services.AddTransient<CompleteVisitHandler>();
        services.AddTransient<AddVisitPhotoHandler>();
        services.AddTransient<GetVanStockHandler>();
        services.AddTransient<GetPartCatalogHandler>();
        services.AddTransient<GetWarehousesHandler>();
        services.AddTransient<ReservePartHandler>();
        services.AddTransient<TokenRefresher>();
        services.AddTransient<RestoreSessionHandler>();
        services.AddTransient<GetVisitProtocolHandler>();
        services.AddTransient<GetMyWorkOrdersHandler>();
        return services;
    }
}
