using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Port: brama do modelu Magazyn (auto technika)
public interface IInventoryGateway
{
    Task<Result<IReadOnlyList<VanStockItem>>> GetVanStockAsync(CancellationToken ct);
}
