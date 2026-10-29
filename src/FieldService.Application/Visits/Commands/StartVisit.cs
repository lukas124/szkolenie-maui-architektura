using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Commands;

public sealed record StartVisit(VisitId VisitId);

// Przypadek użycia: wczytaj → reguła aplikacyjna (jedna wizyta w toku) → model decyduje (R1) → zapisz → Result.
// Reguła patrzy na kilka wizyt naraz, a agregat zna tylko siebie – dlatego mieszka w handlerze, nie w Visit.
public sealed class StartVisitHandler(IVisitRepository visits, IClock clock)
{
    public async Task<Result> HandleAsync(StartVisit command, CancellationToken ct)
    {
        var visit = await visits.GetAsync(command.VisitId, ct);
        if (visit is null) return VisitErrors.NotFound;
        if (await visits.HasOtherInProgressAsync(visit.Id, ct)) return VisitApplicationErrors.AnotherInProgress;

        var started = visit.Start(clock.Now);
        if (!started.IsSuccess) return started;

        return await visits.SaveAsync(visit, ct);
    }
}
