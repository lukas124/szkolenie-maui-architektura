using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Port: katalog cen części (Magazyn). Skąd dane – pamięć, SQLite czy API – decyduje adapter (Cache-Aside)
public interface IPartsCatalog
{
    Task<Result<IReadOnlyList<CatalogPart>>> GetAsync(CancellationToken ct);
}
