using FieldService.Application.Abstractions;
using FieldService.Infrastructure.Inventory;
using FieldService.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace FieldService.Infrastructure.Caching;

// Wszystkie cache technika w jednym miejscu: katalog części (pamięć i SQLite), odpowiedzi z ETagiem, migawka listy,
// miniatury. Wylogowanie i zmiana technika czyszczą je razem – następna osoba nie zobaczy cudzych danych.
public sealed class LocalCaches(SqliteDb db, IMemoryCache memory, ThumbnailCache thumbnails) : ILocalCaches
{
    public async Task ClearAsync(CancellationToken ct)
    {
        memory.Remove(PartsCatalog.MemoryKey);
        var connection = await db.GetConnectionAsync();
        await connection.RunInTransactionAsync(c =>
        {
            c.Execute("DELETE FROM parts_catalog");
            c.Execute("DELETE FROM http_cache");
            c.Execute("DELETE FROM visit_list_snapshot");
        });
        thumbnails.Clear();
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var rows = await connection.ExecuteScalarAsync<int>(
            "SELECT (SELECT COUNT(*) FROM parts_catalog) + (SELECT COUNT(*) FROM http_cache) + (SELECT COUNT(*) FROM visit_list_snapshot)");
        return rows + thumbnails.Count;
    }
}
