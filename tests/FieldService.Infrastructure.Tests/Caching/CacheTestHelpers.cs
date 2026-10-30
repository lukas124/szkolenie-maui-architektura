using FieldService.Infrastructure.Persistence;

namespace FieldService.Infrastructure.Tests.Caching;

// Pomocnicze operacje dla testów cache na prawdziwej bazie SQLite
internal static class CacheTestHelpers
{
    // ZAŁOŻENIE: SqliteDb jest IAsyncDisposable albo IDisposable – zwalniamy tym, co jest dostępne
    public static async Task DisposeDbAsync(SqliteDb? db)
    {
        switch ((object?)db)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    public static async Task<int> CountRowsAsync(SqliteDb db, string table)
    {
        var connection = await db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table}");
    }

    public static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
