using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Zapytanie: wizyty technika na dziś; filtr opcjonalny (null = bez ograniczenia).
// FromSnapshot = true: tylko migawka z telefonu, bez sieci (lista od razu – stale-while-revalidate)
public sealed record GetTodayVisits(bool OnlyOverdue = false, VisitStatus? Status = null, bool FromSnapshot = false);

// Wynik: wizyty i – gdy pochodzą z migawki (FromSnapshot albo brak sieci) – chwila jej zapisu (null = świeże z serwera)
public sealed record TodayVisits(IReadOnlyList<VisitSummary> Visits, DateTimeOffset? SnapshotAt = null);

public static class VisitsErrors
{
    // lista nie była jeszcze pobrana – migawki nie ma
    public static readonly Error NoSnapshot = new("Visits.NoSnapshot", ErrorType.NotFound);
}

// Model odczytu – prosta struktura, która przechodzi przez granicę przypadku użycia (bez DTO z API)
public sealed record VisitSummary(
    Guid Id,
    string WorkOrderNumber,
    VisitStatus Status,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    string CustomerName,
    string Address,
    string DeviceName,
    string FaultDescription,
    bool IsBrigade,
    string ThumbnailUrl,
    int Version,
    bool IsLate = false);
