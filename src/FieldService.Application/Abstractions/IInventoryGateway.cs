using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Port: brama do modelu Magazyn (auto technika, katalog części)
public interface IInventoryGateway
{
    Task<Result<IReadOnlyList<VanStockItem>>> GetVanStockAsync(CancellationToken ct);

    // Katalog części z cenami – do protokołu wizyty
    Task<Result<IReadOnlyList<CatalogPart>>> GetPartCatalogAsync(CancellationToken ct);
}
