using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Przypadek użycia: wizyty na dziś od najwcześniejszej. Spóźniona = zaplanowana, a okno już się zaczęło
public sealed class GetTodayVisitsHandler(IVisitsGateway gateway, IClock clock)
{
    public async Task<Result<IReadOnlyList<VisitSummary>>> HandleAsync(GetTodayVisits query, CancellationToken ct)
    {
        var result = await gateway.GetTodayAsync(ct);
        if (!result.IsSuccess)
            return result;

        var now = clock.Now;
        IReadOnlyList<VisitSummary> visits = result.Value
            .OrderBy(v => v.PlannedStart)
            .Select(v => v with { IsLate = v.Status == VisitStatus.Planned && v.PlannedStart < now })
            .ToList();
        return Result<IReadOnlyList<VisitSummary>>.Success(visits);
    }
}
