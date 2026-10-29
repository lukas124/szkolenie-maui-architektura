using FieldService.Presentation.Orders;
using FieldService.UI.Navigation;
using FieldService.UI.Orders.Pages;

namespace FieldService.UI.Orders;

// Strony modułu Orders w rejestrze stron
public static class OrdersPages
{
    public static IServiceCollection AddOrdersPages(this IServiceCollection services) => services
        .AddPage<WorkOrdersPage, WorkOrdersViewModel>();
}
