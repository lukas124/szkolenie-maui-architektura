using FieldService.Presentation.Common;

namespace FieldService.Presentation.Navigation;

// Nawigacja ViewModel-first: ViewModel mówi „pokaż szczegóły tej wizyty” typem docelowego ViewModelu,
// a nie stroną MAUI. Którą stronę pokazać, wie tylko UI (NavigationService). Wersja minimalna – pełna w checkpoincie 09-navigation.
public interface INavigationService
{
    // Strona dla TViewModel na stos, parameter trafia do OnNavigatedToAsync nowego ViewModelu
    Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : BaseViewModel;

    // Stos od nowa: strona dla TViewModel zostaje jedyną (np. lista po zalogowaniu – „wstecz” nie wraca do logowania)
    Task ResetToAsync<TViewModel>() where TViewModel : BaseViewModel;

    // Poprzednia strona
    Task GoBackAsync();
}
