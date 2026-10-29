using FieldService.Presentation.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Presentation.Orders;

// Moduł Orders w pierścieniu prezentacji: „Moje zlecenia”
public static class OrdersModule
{
    public static IServiceCollection AddOrdersPresentation(this IServiceCollection services, RouteMap routes)
    {
        services.AddTransient<WorkOrdersViewModel>();
        routes.Map<WorkOrdersRoute, WorkOrdersViewModel>();
        return services;
    }
}
