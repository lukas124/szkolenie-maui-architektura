using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Parts.Queries;

public sealed record Warehouse(string Id, string Name);

public sealed record GetWarehouses;

public sealed class GetWarehousesHandler(IInventoryGateway inventory)
{
    public Task<Result<IReadOnlyList<Warehouse>>> HandleAsync(GetWarehouses query, CancellationToken ct) =>
        inventory.GetWarehousesAsync(ct);
}
