using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Parts.Queries;

public sealed record GetPartCatalog;

// Katalog części do wyboru – po nazwie
public sealed class GetPartCatalogHandler(IInventoryGateway inventory)
{
    public async Task<Result<IReadOnlyList<CatalogPart>>> HandleAsync(GetPartCatalog query, CancellationToken ct)
    {
        var catalog = await inventory.GetPartCatalogAsync(ct);
        if (!catalog.IsSuccess) return catalog;

        IReadOnlyList<CatalogPart> sorted = catalog.Value.OrderBy(p => p.Name).ToList();
        return Result<IReadOnlyList<CatalogPart>>.Success(sorted);
    }
}
