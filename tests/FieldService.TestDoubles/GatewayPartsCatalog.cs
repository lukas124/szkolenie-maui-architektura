using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;

namespace FieldService.TestDoubles;

// Katalog cen bez cache – prosto z bramy (testy handlerów i ViewModeli, w których katalog daje atrapa IInventoryGateway)
public sealed class GatewayPartsCatalog(IInventoryGateway inventory) : IPartsCatalog
{
    public Task<Result<IReadOnlyList<CatalogPart>>> GetAsync(CancellationToken ct) => inventory.GetPartCatalogAsync(ct);
}
