using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Session;
using FieldService.Presentation.Visits;
using FieldService.UI.Session.Pages;
using FieldService.UI.Visits.Pages;

namespace FieldService.UI.Navigation;

// Nawigacja ViewModel-first na jednym stosie stron okna (NavigationPage z App.CreateWindow).
// Stronę i ViewModel usługa bierze z kontenera i sama przypisuje ViewModel do strony (BindingContext).
// Wersja minimalna: mapa ViewModel → strona w kodzie. Rejestr stron w DI, modale i trasy dochodzą później.
public sealed class NavigationService(IServiceProvider services) : INavigationService
{
    private static readonly Dictionary<Type, Type> Pages = new()
    {
        [typeof(LoginViewModel)] = typeof(LoginPage),
        [typeof(VisitsListViewModel)] = typeof(VisitsListPage),
        [typeof(VisitDetailsViewModel)] = typeof(VisitDetailsPage)
    };

    // true = trwa zmiana stosu, kolejne wywołanie (np. drugie szybkie tapnięcie) jest wtedy ignorowane
    private bool _isNavigating;

    // Okno tworzy MAUI później niż tę usługę i może je utworzyć ponownie – nawigację bierzemy z bieżącego okna
    private static INavigation CurrentNavigation =>
        Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
        ?? throw new InvalidOperationException("Okno nie ma strony.");

    public async Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : BaseViewModel
    {
        if (_isNavigating) return;
        _isNavigating = true;
        BaseViewModel viewModel;
        try
        {
            var page = CreatePage<TViewModel>(out viewModel);
            await MainThread.InvokeOnMainThreadAsync(() => CurrentNavigation.PushAsync(page));
        }
        finally
        {
            _isNavigating = false;
        }

        await LoadAsync(viewModel, parameter);
    }

    public async Task ResetToAsync<TViewModel>() where TViewModel : BaseViewModel
    {
        if (_isNavigating) return;
        _isNavigating = true;
        BaseViewModel viewModel;
        try
        {
            var page = CreatePage<TViewModel>(out viewModel);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await CurrentNavigation.PushAsync(page, false);
                // stare strony usuwamy dopiero PO dodaniu nowej – stos ani na chwilę nie jest pusty
                foreach (var oldPage in CurrentNavigation.NavigationStack.Where(p => p != page).ToList())
                    CurrentNavigation.RemovePage(oldPage);
            });
        }
        finally
        {
            _isNavigating = false;
        }

        await LoadAsync(viewModel, null);
    }

    public Task GoBackAsync() =>
        MainThread.InvokeOnMainThreadAsync(() =>
            CurrentNavigation.NavigationStack.Count > 1 ? CurrentNavigation.PopAsync() : Task.CompletedTask);

    // Strona startowa dla App.CreateWindow: tworzona tak samo, ale bez zmiany stosu (okna jeszcze nie ma)
    public Page CreateStartPage<TViewModel>() where TViewModel : BaseViewModel => CreatePage<TViewModel>(out _);

    // Strona nie pobiera ViewModelu sama: oba obiekty tworzy kontener, a łączy je usługa nawigacji
    private Page CreatePage<TViewModel>(out BaseViewModel viewModel) where TViewModel : BaseViewModel
    {
        var page = (Page)services.GetRequiredService(Pages[typeof(TViewModel)]);
        viewModel = services.GetRequiredService<TViewModel>();
        page.BindingContext = viewModel;
        return page;
    }

    // Strona jest już widoczna, dane ładują się pod wskaźnikiem ładowania (IsBusy)
    private static async Task LoadAsync(BaseViewModel viewModel, object? parameter)
    {
        viewModel.IsBusy = true;
        try
        {
            await viewModel.OnNavigatedToAsync(parameter);
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }
}
