namespace FieldService.Domain.Visits;

// Status wizyty: Planned → InProgress → Completed; Planned/InProgress → Cancelled (decyzja dyspozytora)
public enum VisitStatus
{
    Planned,
    InProgress,
    Completed,
    Cancelled
}
