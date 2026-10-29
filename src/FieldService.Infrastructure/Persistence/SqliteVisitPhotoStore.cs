using System.Globalization;
using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;

namespace FieldService.Infrastructure.Persistence;

// Zdjęcia wizyt (ścieżki plików) w osobnej tabeli – poza agregatem wizyty
public sealed class SqliteVisitPhotoStore(SqliteDb db) : IVisitPhotoStore
{
    public async Task AddAsync(VisitId visitId, string filePath, DateTimeOffset takenAt, CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        await connection.ExecuteAsync("INSERT INTO visit_photos (VisitId, FilePath, TakenAt) VALUES (?, ?, ?)",
            visitId.Value.ToString(), filePath, takenAt.ToString("O", CultureInfo.InvariantCulture));
    }

    public async Task<int> CountAsync(VisitId visitId, CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM visit_photos WHERE VisitId = ?", visitId.Value.ToString());
    }
}
