using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Przypadek użycia: wizyty na dziś od najwcześniejszej. Wizyta zmieniona w telefonie (niewysłane zmiany technika)
// ma status z lokalnej repliki. Spóźniona = zaplanowana, a okno już się zaczęło.
public sealed class GetTodayVisitsHandler(IVisitsGateway gateway, IVisitReplica replica, IClock clock)
{
    public async Task<Result<IReadOnlyList<VisitSummary>>> HandleAsync(GetTodayVisits query, CancellationToken ct)
    {
        var result = await gateway.GetTodayAsync(ct);
        if (!result.IsSuccess)
            return result;

        var changed = await replica.GetLocallyChangedStatusesAsync(ct);
        var now = clock.Now;
        var list = result.Value
            .OrderBy(v => v.PlannedStart)
            .Select(v => changed.TryGetValue(v.Id, out var status) ? v with { Status = status } : v)
            .Select(v => v with { IsLate = v.Status == VisitStatus.Planned && v.PlannedStart < now })
            .ToList();
        return Result<IReadOnlyList<VisitSummary>>.Success(list);
    }
}
