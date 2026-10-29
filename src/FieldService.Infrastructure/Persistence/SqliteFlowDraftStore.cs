using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;

namespace FieldService.Infrastructure.Persistence;

// Szkic kreatora w SQLite: jeden wiersz (Id = 1) – zapis nadpisuje, wyczyszczenie usuwa
public sealed class SqliteFlowDraftStore(SqliteDb db) : IFlowDraftStore
{
    private sealed class DraftRow
    {
        public string VisitId { get; set; } = "";
        public int StepIndex { get; set; }
    }

    public async Task SaveAsync(FlowDraft draft, CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        await connection.ExecuteAsync("INSERT OR REPLACE INTO flow_drafts (Id, VisitId, StepIndex) VALUES (1, ?, ?)",
            draft.VisitId.Value.ToString(), draft.StepIndex);
    }

    public async Task<FlowDraft?> GetAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var rows = await connection.QueryAsync<DraftRow>("SELECT VisitId, StepIndex FROM flow_drafts WHERE Id = 1");
        return rows.FirstOrDefault() is { } row ? new FlowDraft(new VisitId(Guid.Parse(row.VisitId)), row.StepIndex) : null;
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM flow_drafts");
    }
}
