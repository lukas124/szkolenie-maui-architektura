using FieldService.Application.Abstractions;
using SQLite;

namespace FieldService.Infrastructure.Persistence;

// Jedno połączenie z bazą aplikacji (plik w katalogu danych); migracje przy pierwszym użyciu.
// Nieudane otwarcie nie jest zapamiętywane – następne wywołanie próbuje ponownie.
public sealed class SqliteDb(IAppPaths paths)
{
    private Task<SQLiteAsyncConnection>? _connection;

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        var opening = _connection ??= OpenAsync(paths);
        try
        {
            return await opening;
        }
        catch
        {
            _ = Interlocked.CompareExchange(ref _connection, null, opening);
            throw;
        }
    }

    private static async Task<SQLiteAsyncConnection> OpenAsync(IAppPaths paths)
    {
        Directory.CreateDirectory(paths.DataDirectory);
        // inny plik niż w 01-typowe-mvvm (tam tabela visits bez migracji) – ta sama aplikacja na tym samym telefonie
        var connection = new SQLiteAsyncConnection(Path.Combine(paths.DataDirectory, "fieldservice-replica.db3"));
        await SqliteMigrations.ApplyAsync(connection);
        return connection;
    }
}
