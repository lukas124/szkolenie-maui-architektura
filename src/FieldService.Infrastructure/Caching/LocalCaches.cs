using FieldService.Application.Abstractions;
using FieldService.Infrastructure.Persistence;

namespace FieldService.Infrastructure.Caching;

// Cache technika: na razie tylko migawka listy wizyt (Blok 11). Wylogowanie i zmiana technika czyszczą ją –
// następna osoba nie zobaczy cudzych danych.
public sealed class LocalCaches(SqliteDb db) : ILocalCaches
{
    public async Task ClearAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM visit_list_snapshot");
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM visit_list_snapshot");
    }
}
