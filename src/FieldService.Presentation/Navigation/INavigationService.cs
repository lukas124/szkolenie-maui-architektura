namespace FieldService.Presentation.Navigation;

// Nawigacja ViewModel-first: ViewModel prosi o cel (trasę), usługa nawigacji (UI) dobiera ViewModel z RouteMap
// i stronę z rejestru stron, wstawia ją na jeden stos okna i woła haki. Wersja pełna (Blok 09).
public interface INavigationService
{
    // Strona trasy na stos
    Task<NavigationResult> NavigateAsync<TRoute>(TRoute route) where TRoute : IRoute;

    // Strona trasy jako modal, we własnym zakresie kontenera (Scoped = „na przepływ”, np. szkic kreatora)
    Task<NavigationResult> OpenModalAsync<TRoute>(TRoute route) where TRoute : IRoute;

    // Zamyka górny modal (zakres przepływu jest sprzątany)
    Task CloseModalAsync();

    // Wstecz: najpierw modal, potem stos
    Task GoBackAsync();

    // Stos od nowa: strona trasy zostaje jedyną (np. lista po zalogowaniu – „wstecz” nie wraca do logowania)
    Task<NavigationResult> ResetToAsync<TRoute>(TRoute route) where TRoute : IRoute;
}
