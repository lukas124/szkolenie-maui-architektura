using FieldService.Presentation.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Presentation.Parts;

// Moduł Parts w pierścieniu prezentacji: „Auto technika”
public static class PartsModule
{
    public static IServiceCollection AddPartsPresentation(this IServiceCollection services, RouteMap routes)
    {
        services.AddTransient<VanStockViewModel>();
        routes.Map<VanStockRoute, VanStockViewModel>();
        return services;
    }
}
