using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Commands;

public sealed record RecordActivity(VisitId VisitId, string Code, string Description);

public sealed class RecordActivityHandler(IVisitRepository visits, IClock clock)
{
    public async Task<Result> HandleAsync(RecordActivity command, CancellationToken ct)
    {
        var visit = await visits.GetAsync(command.VisitId, ct);
        if (visit is null) return VisitErrors.NotFound;

        var recorded = visit.RecordActivity(command.Code, command.Description, clock.Now);
        if (!recorded.IsSuccess) return recorded;

        return await visits.SaveAsync(visit, ct);
    }
}
