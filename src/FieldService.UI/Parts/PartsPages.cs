using FieldService.Presentation.Parts;
using FieldService.UI.Navigation;
using FieldService.UI.Parts.Pages;

namespace FieldService.UI.Parts;

// Strony modułu Parts w rejestrze stron
public static class PartsPages
{
    public static IServiceCollection AddPartsPages(this IServiceCollection services) => services
        .AddPage<VanStockPage, VanStockViewModel>();
}
