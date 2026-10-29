using System.Globalization;
using System.Text.Json;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;

namespace FieldService.Infrastructure.Persistence;

// Migawka listy wizyt w SQLite: jeden wiersz (Id = 1), lista jako JSON – zapis nadpisuje
public sealed class SqliteVisitListSnapshotStore(SqliteDb db) : IVisitListSnapshotStore
{
    private sealed class SnapshotRow
    {
        public string VisitsJson { get; set; } = "";
        public string SavedAt { get; set; } = "";
    }

    public async Task SaveAsync(VisitListSnapshot snapshot, CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        await connection.ExecuteAsync("INSERT OR REPLACE INTO visit_list_snapshot (Id, VisitsJson, SavedAt) VALUES (1, ?, ?)",
            JsonSerializer.Serialize(snapshot.Visits), snapshot.SavedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    public async Task<VisitListSnapshot?> GetAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var rows = await connection.QueryAsync<SnapshotRow>("SELECT VisitsJson, SavedAt FROM visit_list_snapshot WHERE Id = 1");
        if (rows.FirstOrDefault() is not { } row) return null;

        var visits = JsonSerializer.Deserialize<List<VisitSummary>>(row.VisitsJson) ?? [];
        return new VisitListSnapshot(visits, DateTimeOffset.Parse(row.SavedAt, CultureInfo.InvariantCulture));
    }
}
