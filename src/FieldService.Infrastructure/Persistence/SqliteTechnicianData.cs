using FieldService.Application.Abstractions;

namespace FieldService.Infrastructure.Persistence;

// Dane technika w bazie telefonu. Zmiany jeszcze nie wychodzą z telefonu (wysyłka dopiero w Bloku 11), więc nie ma
// czego chronić przed następnym technikiem – logowanie innego technika po prostu czyści dane poprzedniego.
// Czyszczenie usuwa wizyty i zdjęcia (wiersze i pliki).
public sealed class SqliteTechnicianData(SqliteDb db) : ILocalTechnicianData
{
    public Task<bool> HasUnsentChangesAsync(CancellationToken ct) => Task.FromResult(false);

    public async Task ClearAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var photos = await connection.QueryScalarsAsync<string>("SELECT FilePath FROM visit_photos");
        await connection.RunInTransactionAsync(c =>
        {
            c.Execute("DELETE FROM visits");
            c.Execute("DELETE FROM visit_photos");
        });
        foreach (var path in photos) DeleteFile(path);
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
