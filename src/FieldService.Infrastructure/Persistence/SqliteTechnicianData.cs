using FieldService.Application.Abstractions;

namespace FieldService.Infrastructure.Persistence;

// Dane technika w bazie telefonu: niewysłane zmiany = wiadomości czekające w outbox.
// Czyszczenie usuwa wizyty, zdjęcia (wiersze i pliki), szkic kreatora, kolejkę (same wysłane albo odrzucone)
// i migawkę listy, a na końcu cache technika (ILocalCaches).
public sealed class SqliteTechnicianData(SqliteDb db, ILocalCaches caches) : ILocalTechnicianData
{
    public async Task<bool> HasUnsentChangesAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox WHERE SentAt IS NULL AND Failed = 0") > 0;
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var photos = await connection.QueryScalarsAsync<string>("SELECT FilePath FROM visit_photos");
        await connection.RunInTransactionAsync(c =>
        {
            c.Execute("DELETE FROM visits");
            c.Execute("DELETE FROM visit_photos");
            c.Execute("DELETE FROM flow_drafts");
            c.Execute("DELETE FROM outbox");
            c.Execute("DELETE FROM visit_list_snapshot");
        });
        foreach (var path in photos) DeleteFile(path);
        await caches.ClearAsync(ct);
    }

    // Plik zdjęcia mógł zniknąć albo być zablokowany – logowanie nie może się przez to wywrócić
    private static void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
