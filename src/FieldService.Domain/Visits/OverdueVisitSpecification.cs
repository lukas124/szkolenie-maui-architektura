namespace FieldService.Domain.Visits;

// Specyfikacja (DDD): „spóźniona wizyta” = zaplanowana, a okno już się zaczęło. Jedna definicja dla znaczka na liście
// i filtra „Tylko spóźnione” – zmienia się zapytanie, nie reguła.
public sealed class OverdueVisitSpecification(DateTimeOffset now)
{
    public bool IsSatisfiedBy(VisitStatus status, DateTimeOffset plannedStart) =>
        status == VisitStatus.Planned && plannedStart < now;
}
