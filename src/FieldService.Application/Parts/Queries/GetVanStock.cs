using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Parts.Queries;

// Model odczytu: część w aucie technika
public sealed record VanStockItem(string PartId, string Name, int Quantity);

public sealed record GetVanStock;

public sealed class GetVanStockHandler(IInventoryGateway inventory)
{
    public Task<Result<IReadOnlyList<VanStockItem>>> HandleAsync(GetVanStock query, CancellationToken ct) =>
        inventory.GetVanStockAsync(ct);
}
