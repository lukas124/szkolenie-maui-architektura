using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;

namespace FieldService.Infrastructure.Inventory;

// Katalog cen części – na razie zawsze z API: każde wejście w „Części” to żądanie HTTP, bez sieci katalogu nie ma.
public sealed class PartsCatalog(IInventoryGateway gateway) : IPartsCatalog
{
    public Task<Result<IReadOnlyList<CatalogPart>>> GetAsync(CancellationToken ct) => gateway.GetPartCatalogAsync(ct);
}
