using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Zapytanie: wizyty technika na dziś; filtr opcjonalny (null = bez ograniczenia)
public sealed record GetTodayVisits(bool OnlyOverdue = false, VisitStatus? Status = null);

// Wynik: wizyty i – gdy nie było sieci – chwila pobrania migawki, z której pochodzą (null = świeże z serwera)
public sealed record TodayVisits(IReadOnlyList<VisitSummary> Visits, DateTimeOffset? OfflineSince = null);

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
