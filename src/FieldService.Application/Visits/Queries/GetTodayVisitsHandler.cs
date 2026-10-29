using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Przypadek użycia: wizyty na dziś od najwcześniejszej. Wizyta zmieniona w telefonie (niewysłane zmiany technika)
// ma status z lokalnej repliki. Spóźnioną wskazuje OverdueVisitSpecification; filtr działa po nałożeniu statusu.
// Każda pobrana lista trafia do migawki; bez sieci lista pochodzi z migawki (z chwilą jej pobrania).
public sealed class GetTodayVisitsHandler(IVisitsGateway gateway, IVisitReplica replica, IVisitListSnapshotStore snapshots,
    IClock clock)
{
    public async Task<Result<TodayVisits>> HandleAsync(GetTodayVisits query, CancellationToken ct)
    {
        var result = await gateway.GetTodayAsync(ct);
        IReadOnlyList<VisitSummary> visits;
        DateTimeOffset? offlineSince = null;
        if (result.IsSuccess)
        {
            visits = result.Value;
            await snapshots.SaveAsync(new VisitListSnapshot(visits, clock.Now), ct);
        }
        else if (result.Error!.Type == ErrorType.Network && await snapshots.GetAsync(ct) is { } snapshot)
        {
            visits = snapshot.Visits;
            offlineSince = snapshot.SavedAt;
        }
        else
        {
            return result.Error!;
        }

        var changed = await replica.GetLocallyChangedStatusesAsync(ct);
        var overdue = new OverdueVisitSpecification(clock.Now);
        var list = visits
            .OrderBy(v => v.PlannedStart)
            .Select(v => changed.TryGetValue(v.Id, out var status) ? v with { Status = status } : v)
            .Select(v => v with { IsLate = overdue.IsSatisfiedBy(v.Status, v.PlannedStart) })
            .Where(v => !query.OnlyOverdue || v.IsLate)
            .Where(v => query.Status is null || v.Status == query.Status)
            .ToList();
        return Result<TodayVisits>.Success(new TodayVisits(list, offlineSince));
    }
}
