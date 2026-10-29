using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Zapytanie: historia – zakończone i anulowane wizyty z poprzednich dni, od najnowszej; strony od 1
public sealed record GetVisitHistory(int Page, int PageSize = 30);

// Model odczytu wpisu historii (PhotoUrl – pełny obraz, ThumbnailUrl – miniatura do listy)
public sealed record VisitHistoryEntry(Guid Id, string WorkOrderNumber, VisitStatus Status, DateTimeOffset PlannedStart,
    DateTimeOffset? CompletedAt, string CustomerName, string City, string DeviceName, VisitOutcomeType? Outcome,
    string ThumbnailUrl, string PhotoUrl);

// Jedna strona historii; TotalCount – wszystkie wpisy (wszystkie strony)
public sealed record VisitHistoryPageResult(IReadOnlyList<VisitHistoryEntry> Items, int Page, int TotalCount);

public sealed class GetVisitHistoryHandler(IVisitsGateway gateway)
{
    public Task<Result<VisitHistoryPageResult>> HandleAsync(GetVisitHistory query, CancellationToken ct) =>
        gateway.GetHistoryAsync(query.Page, query.PageSize, ct);
}
