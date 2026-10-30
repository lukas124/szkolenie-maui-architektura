using FieldService.Domain.Visits;

namespace FieldService.Presentation.Navigation;

// Trasy aplikacji – kolejne dochodzą razem z ekranami
public sealed record LoginRoute : IRoute;

public sealed record VisitsListRoute : IRoute;

public sealed record VisitDetailsRoute(VisitId Id) : IRoute;

// Kreator zakończenia wizyty – otwierany modalnie
public sealed record CompleteVisitRoute(VisitId Id) : IRoute;

// Filtr listy wizyt – modal
public sealed record VisitFilterRoute : IRoute;

// „Moje zlecenia” (moduł Orders)
public sealed record WorkOrdersRoute : IRoute;

// Trasy są wspólne dla modułów (nie należą do żadnego) – kierunek nawigacji między modułami pilnuje przegląd,
// test granic (ModuleBoundaryTests) widzi tylko zależności od typów modułu w parametrach trasy

// Historia wizyt (moduł Visits)
public sealed record VisitHistoryRoute : IRoute;

// „Auto technika” – części w aucie (moduł Parts)
public sealed record VanStockRoute : IRoute;

// Rezerwacja części z magazynu dla wizyty – modal (moduł Parts)
public sealed record ReservePartRoute(Guid VisitId) : IRoute;
