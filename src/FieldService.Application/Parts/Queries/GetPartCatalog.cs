using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Parts.Queries;

public sealed record GetPartCatalog;

// Katalog części do wyboru – po nazwie
public sealed class GetPartCatalogHandler(IPartsCatalog catalog)
{
    public async Task<Result<IReadOnlyList<CatalogPart>>> HandleAsync(GetPartCatalog query, CancellationToken ct)
    {
        var parts = await catalog.GetAsync(ct);
        if (!parts.IsSuccess) return parts;

        IReadOnlyList<CatalogPart> sorted = parts.Value.OrderBy(p => p.Name).ToList();
        return Result<IReadOnlyList<CatalogPart>>.Success(sorted);
    }
}
