using System.Globalization;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Api;
using FieldService.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace FieldService.Infrastructure.Inventory;

// Katalog cen części – odkładanie na żądanie (Cache-Aside): pamięć (TTL) → API → zapis w SQLite.
// Bez sieci katalog z SQLite: działa offline i po ubiciu procesu. Ceny zmieniają się rzadko – kilka minut starości
// nie szkodzi, a 10 wejść w „Części” to jedno żądanie zamiast dziesięciu.
public sealed class PartsCatalog(
    IInventoryGateway gateway, INetworkStatus network, SqliteDb db, IMemoryCache memory, CachingOptions options)
    : IPartsCatalog
{
    public const string MemoryKey = "parts-catalog";

    private sealed class CatalogRow
    {
        public string PartId { get; set; } = "";
        public string Name { get; set; } = "";
        public string UnitPrice { get; set; } = "";
    }

    public async Task<Result<IReadOnlyList<CatalogPart>>> GetAsync(CancellationToken ct)
    {
        if (memory.TryGetValue(MemoryKey, out IReadOnlyList<CatalogPart>? cached) && cached is not null)
            return Result<IReadOnlyList<CatalogPart>>.Success(cached);

        // telefon bez internetu: od razu SQLite – bez żądania HTTP i bez czekania na ponowienia Polly
        var result = network.IsOnline
            ? await gateway.GetPartCatalogAsync(ct)
            : Result<IReadOnlyList<CatalogPart>>.Failure(ApiErrors.Network);
        if (result.IsSuccess)
        {
            await SaveAsync(result.Value);
            memory.Set(MemoryKey, result.Value, options.CatalogTtl);
            return result;
        }

        // bez sieci – ostatni katalog z telefonu (nie wkładamy go do pamięci: po powrocie sieci zapytamy API)
        if (result.Error!.Type == ErrorType.Network && await LoadAsync() is { Count: > 0 } stored)
            return Result<IReadOnlyList<CatalogPart>>.Success(stored);
        return result;
    }

    public void Clear() => memory.Remove(MemoryKey);

    private async Task SaveAsync(IReadOnlyList<CatalogPart> parts)
    {
        var connection = await db.GetConnectionAsync();
        await connection.RunInTransactionAsync(c =>
        {
            c.Execute("DELETE FROM parts_catalog");
            foreach (var part in parts)
                c.Execute("INSERT INTO parts_catalog (PartId, Name, UnitPrice) VALUES (?, ?, ?)",
                    part.PartId, part.Name, part.UnitPrice.ToString(CultureInfo.InvariantCulture));
        });
    }

    private async Task<IReadOnlyList<CatalogPart>> LoadAsync()
    {
        var connection = await db.GetConnectionAsync();
        var rows = await connection.QueryAsync<CatalogRow>("SELECT PartId, Name, UnitPrice FROM parts_catalog");
        return rows.Select(r => new CatalogPart(r.PartId, r.Name, decimal.Parse(r.UnitPrice, CultureInfo.InvariantCulture))).ToList();
    }
}
