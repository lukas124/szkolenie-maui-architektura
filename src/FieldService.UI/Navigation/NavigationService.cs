using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.UI.Pages;
using Microsoft.Extensions.Logging;

namespace FieldService.UI.Navigation;

// Nawigacja ViewModel-first na jednym stosie stron okna (NavigationPage z App.CreateWindow), bez Shella.
// Trasa → ViewModel (RouteMap, wpisy z modułów) → strona (PageRegistry). Stronę i ViewModel usługa bierze
// z kontenera, przypisuje ViewModel do strony (BindingContext), wstawia stronę na stos i woła hak INavigatedTo.
// Usługa nawigacji należy do korzenia kompozycji: tylko ona i App znają kontener.
public sealed class NavigationService(IServiceProvider services, IServiceScopeFactory scopes, RouteMap routes,
    PageRegistry pages, ILogger<NavigationService> logger) : INavigationService
{
    // true = trwa zmiana stosu, kolejne wywołanie (np. drugie szybkie tapnięcie) jest odrzucane, zanim powstanie strona
    private bool _isNavigating;

    // Modal → jego zakres w kontenerze (Scoped „na przepływ”, np. CompletionDraft kreatora)
    private readonly Dictionary<Page, IServiceScope> _modalScopes = [];
    private bool _modalPoppedHooked;

    // Okno tworzy MAUI później niż tę usługę i może je utworzyć ponownie – nawigację bierzemy z bieżącego okna
    private static INavigation CurrentNavigation =>
        Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
        ?? throw new InvalidOperationException("Okno nie ma strony.");

    public Task<NavigationResult> NavigateAsync<TRoute>(TRoute route) where TRoute : IRoute =>
        ChangeStackAsync(route, services, page => CurrentNavigation.PushAsync(page));

    public Task<NavigationResult> ResetToAsync<TRoute>(TRoute route) where TRoute : IRoute =>
        ChangeStackAsync(route, services, async page =>
        {
            var navigation = CurrentNavigation;
            await navigation.PushAsync(page, false);
            // stare strony usuwamy dopiero PO dodaniu nowej – stos ani na chwilę nie jest pusty
            foreach (var oldPage in navigation.NavigationStack.Where(p => p != page).ToList())
            {
                navigation.RemovePage(oldPage);
                (oldPage as BasePage)?.NotifyClosed();
            }
        });

    public async Task<NavigationResult> OpenModalAsync<TRoute>(TRoute route) where TRoute : IRoute
    {
        HookModalPopped();
        var scope = scopes.CreateScope();
        try
        {
            var result = await ChangeStackAsync(route, scope.ServiceProvider, async page =>
            {
                _modalScopes[page] = scope;
                try
                {
                    await CurrentNavigation.PushModalAsync(page);
                }
                catch
                {
                    _modalScopes.Remove(page);
                    throw;
                }
            });
            if (result != NavigationResult.Success) scope.Dispose();
            return result;
        }
        catch
        {
            if (!_modalScopes.ContainsValue(scope)) scope.Dispose(); // np. brak rejestracji – zakres nie może wisieć
            throw;
        }
    }

    // Zakres modala i hak OnClosed obsługuje ModalPopped – tak samo dla przycisku, „wstecz” i strzałki w nagłówku
    public Task CloseModalAsync() =>
        MainThread.InvokeOnMainThreadAsync(() =>
            CurrentNavigation.ModalStack.Count > 0 ? CurrentNavigation.PopModalAsync() : Task.CompletedTask);

    // PopAsync nie zamknie modala – najpierw sprawdzamy stos modalny
    public Task GoBackAsync() =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            var navigation = CurrentNavigation;
            if (navigation.ModalStack.Count > 0) return navigation.PopModalAsync();
            return navigation.NavigationStack.Count > 1 ? navigation.PopAsync() : Task.CompletedTask;
        });

    private async Task<NavigationResult> ChangeStackAsync<TRoute>(TRoute route, IServiceProvider provider, Func<Page, Task> show)
        where TRoute : IRoute
    {
        if (_isNavigating)
        {
            logger.LogDebug("Nawigacja do {Route} odrzucona: trwa inna nawigacja", route);
            return NavigationResult.Rejected;
        }

        if (routes.ViewModelFor(route.GetType()) is not { } viewModelType
            || pages.PageFor(viewModelType) is not { } pageType)
        {
            logger.LogWarning("Brak trasy {Route} w RouteMap albo strony w rejestrze", route);
            return NavigationResult.NoRoute;
        }

        _isNavigating = true;
        BaseViewModel viewModel;
        try
        {
            var page = CreatePage(pageType, viewModelType, provider, out viewModel);
            await MainThread.InvokeOnMainThreadAsync(() => show(page));
        }
        finally
        {
            _isNavigating = false;
        }

        await LoadAsync(viewModel, route);
        return NavigationResult.Success;
    }

    // Strona startowa dla App.CreateWindow: tworzona tak samo, ale bez zmiany stosu (okna jeszcze nie ma)
    public Page CreateStartPage<TRoute>() where TRoute : IRoute
    {
        if (routes.ViewModelFor(typeof(TRoute)) is not { } viewModelType
            || pages.PageFor(viewModelType) is not { } pageType)
            throw new InvalidOperationException($"Brak trasy {typeof(TRoute).Name} w RouteMap albo strony w rejestrze.");
        return CreatePage(pageType, viewModelType, services, out _);
    }

    // Strona nie pobiera ViewModelu sama: oba obiekty tworzy kontener (dla modala jego zakres),
    // a łączy je usługa nawigacji
    private static Page CreatePage(Type pageType, Type viewModelType, IServiceProvider provider, out BaseViewModel viewModel)
    {
        var page = (Page)provider.GetRequiredService(pageType);
        viewModel = (BaseViewModel)provider.GetRequiredService(viewModelType);
        page.BindingContext = viewModel;
        return page;
    }

    // Najpierw push, potem ładowanie: strona już widoczna, dane ładują się pod wskaźnikiem (IsBusy)
    private static async Task LoadAsync<TRoute>(BaseViewModel viewModel, TRoute route) where TRoute : IRoute
    {
        if (viewModel is not INavigatedTo<TRoute> target) return;

        viewModel.IsBusy = true;
        try
        {
            await target.OnNavigatedToAsync(route);
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }

    private void HookModalPopped()
    {
        if (_modalPoppedHooked || Microsoft.Maui.Controls.Application.Current is not { } app) return;
        app.ModalPopped += (_, e) =>
        {
            if (!_modalScopes.Remove(e.Modal, out var scope)) return; // np. DialogPage – bez własnego zakresu
            (e.Modal as BasePage)?.NotifyClosed();
            scope.Dispose();
        };
        _modalPoppedHooked = true;
    }
}
