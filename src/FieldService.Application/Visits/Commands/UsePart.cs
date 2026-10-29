using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Commands;

public sealed record UsePart(VisitId VisitId, PartId PartId, int Quantity);

public sealed class UsePartHandler(IVisitRepository visits, IClock clock)
{
    public async Task<Result> HandleAsync(UsePart command, CancellationToken ct)
    {
        var visit = await visits.GetAsync(command.VisitId, ct);
        if (visit is null) return VisitErrors.NotFound;

        var quantity = Quantity.Create(command.Quantity);
        if (!quantity.IsSuccess) return quantity.Error!;

        var used = visit.UsePart(command.PartId, quantity.Value, clock.Now);
        if (!used.IsSuccess) return used;

        return await visits.SaveAsync(visit, ct);
    }
}
