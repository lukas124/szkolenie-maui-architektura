using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Zapytanie: wizyty technika na dziś
public sealed record GetTodayVisits;

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
