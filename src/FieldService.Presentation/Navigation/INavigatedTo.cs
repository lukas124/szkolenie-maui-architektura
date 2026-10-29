namespace FieldService.Presentation.Navigation;

// ViewModel, który przyjmuje parametr trasy – typowany, bez object? i rzutowania
public interface INavigatedTo<in TRoute> where TRoute : IRoute
{
    // Raz, po wejściu na stronę (strona już widoczna, pod wskaźnikiem ładowania)
    Task OnNavigatedToAsync(TRoute route);
}
