using FieldService.Presentation.Common;

namespace FieldService.UI.Navigation;

// Wpis rejestru stron: ViewModel → strona (dodaje go AddPage)
public sealed record PageRegistration(Type ViewModel, Type Page);

// Rejestr stron z kontenera: bez statycznej mapy i bez Activator.CreateInstance – strona może mieć zależności
// w konstruktorze, a rejestr nie ma stanu globalnego
public sealed class PageRegistry(IEnumerable<PageRegistration> registrations)
{
    private readonly Dictionary<Type, Type> _pages = registrations.ToDictionary(r => r.ViewModel, r => r.Page);

    // null = ViewModel bez strony
    public Type? PageFor(Type viewModel) => _pages.GetValueOrDefault(viewModel);
}

public static class PageRegistryExtensions
{
    // Strona (Transient) + wpis rejestru; ViewModel rejestruje jego moduł w Presentation
    public static IServiceCollection AddPage<TPage, TViewModel>(this IServiceCollection services)
        where TPage : Page where TViewModel : BaseViewModel
    {
        services.AddTransient<TPage>();
        services.AddSingleton(new PageRegistration(typeof(TViewModel), typeof(TPage)));
        return services;
    }
}
