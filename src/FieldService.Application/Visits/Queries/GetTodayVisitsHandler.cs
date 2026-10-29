using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Przypadek użycia: wizyty na dziś od najwcześniejszej. Spóźniona = zaplanowana, a okno już się zaczęło
// TODO: Ćwiczenie 02 – przyjmij port IVisitsGateway w konstruktorze zamiast tworzyć bramę HTTP (DIP)
public sealed class GetTodayVisitsHandler(AccessTokenStore tokens, IClock clock)
{
    private readonly HttpVisitsGateway _gateway = new(
        new HttpClient { BaseAddress = new Uri("https://mobilny.dev/maui/architektura/"), Timeout = TimeSpan.FromSeconds(15) },
        tokens);

    public async Task<Result<IReadOnlyList<VisitSummary>>> HandleAsync(GetTodayVisits query, CancellationToken ct)
    {
        var result = await _gateway.GetTodayAsync(ct);
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
