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
