using FieldService.Presentation.Common;

namespace FieldService.Presentation.Navigation;

// Trasa → ViewModel. Moduły dopisują swoje wpisy (zasada otwarte–zamknięte: NavigationService się nie zmienia).
public sealed class RouteMap
{
    private readonly Dictionary<Type, Type> _entries = [];

    public IReadOnlyDictionary<Type, Type> Entries => _entries;

    public RouteMap Map<TRoute, TViewModel>() where TRoute : IRoute where TViewModel : BaseViewModel
    {
        _entries[typeof(TRoute)] = typeof(TViewModel);
        return this;
    }

    // null = brak wpisu (NavigationResult.NoRoute)
    public Type? ViewModelFor(Type routeType) => _entries.GetValueOrDefault(routeType);
}
