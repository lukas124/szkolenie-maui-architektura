using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Commands;

public sealed record RemoveUsedPart(VisitId VisitId, PartId PartId);

public sealed class RemoveUsedPartHandler(IVisitRepository visits, IClock clock)
{
    public async Task<Result> HandleAsync(RemoveUsedPart command, CancellationToken ct)
    {
        var visit = await visits.GetAsync(command.VisitId, ct);
        if (visit is null) return VisitErrors.NotFound;

        var removed = visit.RemoveUsedPart(command.PartId, clock.Now);
        if (!removed.IsSuccess) return removed;

        return await visits.SaveAsync(visit, ct);
    }
}
