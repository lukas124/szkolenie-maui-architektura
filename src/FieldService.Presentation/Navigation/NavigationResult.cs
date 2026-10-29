namespace FieldService.Presentation.Navigation;

// Wynik nawigacji zamiast cichego return
public enum NavigationResult
{
    Success,
    Rejected, // trwa inna nawigacja (np. drugie szybkie tapnięcie)
    NoRoute   // trasa bez wpisu w RouteMap
}
