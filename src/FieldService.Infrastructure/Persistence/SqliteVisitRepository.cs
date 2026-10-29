using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Infrastructure.Persistence;

// Lokalna replika wizyt w SQLite: repozytorium agregatu (zapis z blokowaniem optymistycznym) i odczyt szczegółów.
// Version – wersja lokalna (dwóch piszących na telefonie), ServerVersion – wersja z ostatniego pobrania z serwera.
public sealed class SqliteVisitRepository(SqliteDb db) : IVisitRepository, IVisitReplica
{
    public async Task<Visit?> GetAsync(VisitId id, CancellationToken ct)
    {
        var row = await FindAsync(id);
        return row is null ? null : VisitRowMapper.ToVisit(row);
    }

    public async Task AddAsync(Visit visit, CancellationToken ct)
    {
        var row = new VisitRow { ServerVersion = visit.Version };
        VisitRowMapper.WriteState(visit, row);
        await InsertAsync(row);
    }

    // UPDATE … WHERE Version = wersja odczytana: jeśli w międzyczasie zapisał ktoś inny, nie zmienia się żaden wiersz
    public async Task<Result> SaveAsync(Visit visit, CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var row = new VisitRow();
        VisitRowMapper.WriteState(visit, row);
        var updated = await connection.ExecuteAsync(
            """
            UPDATE visits SET Status = ?, StartedAt = ?, CompletedAt = ?, ScopeJson = ?, ScopeVersion = ?,
                SignatureJson = ?, OutcomeType = ?, OutcomeReason = ?, CancelReason = ?, Version = ?
            WHERE Id = ? AND Version = ?
            """,
            row.Status, row.StartedAt, row.CompletedAt, row.ScopeJson, row.ScopeVersion,
            row.SignatureJson, row.OutcomeType, row.OutcomeReason, row.CancelReason, visit.Version + 1,
            row.Id, visit.Version);
        if (updated == 0) return VisitErrors.VersionConflict;

        visit.IncrementVersion();
        visit.ClearDomainEvents();
        return Result.Success();
    }

    public async Task<bool> HasOtherInProgressAsync(VisitId visitId, CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM visits WHERE Status = ? AND Id <> ?",
            nameof(VisitStatus.InProgress), visitId.Value.ToString()) > 0;
    }

    public async Task<VisitDetails?> GetDetailsAsync(VisitId id, CancellationToken ct)
    {
        var row = await FindAsync(id);
        return row is null ? null : VisitRowMapper.ToDetails(row);
    }

    // Wiersz bez lokalnych zmian zastępujemy świeżym stanem z serwera, a wiersz ze zmianami technika zostaje
    // (INSERT OR IGNORE) – wysyłka zmian i rozstrzyganie konfliktów to synchronizacja
    public async Task SeedAsync(RemoteVisit visit, CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM visits WHERE Id = ? AND Version = ServerVersion", visit.Visit.Id.Value.ToString());

        var row = new VisitRow { ServerVersion = visit.Visit.Version };
        VisitRowMapper.WriteState(visit.Visit, row);
        VisitRowMapper.WriteSnapshot(visit.Snapshot, row);
        await InsertAsync(row, "INSERT OR IGNORE");
    }

    public async Task<IReadOnlyDictionary<Guid, VisitStatus>> GetLocallyChangedStatusesAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var rows = await connection.QueryAsync<VisitRow>("SELECT Id, Status FROM visits WHERE Version <> ServerVersion");
        return rows.ToDictionary(r => Guid.Parse(r.Id), r => Enum.Parse<VisitStatus>(r.Status));
    }

    private async Task<VisitRow?> FindAsync(VisitId id)
    {
        var connection = await db.GetConnectionAsync();
        var rows = await connection.QueryAsync<VisitRow>("SELECT * FROM visits WHERE Id = ?", id.Value.ToString());
        return rows.FirstOrDefault();
    }

    private async Task InsertAsync(VisitRow row, string insert = "INSERT")
    {
        var connection = await db.GetConnectionAsync();
        await connection.ExecuteAsync(
            insert + """
             INTO visits (Id, WorkOrderId, TechnicianIdsJson, Status, PlannedStart, PlannedEnd, StartedAt, CompletedAt,
                ScopeJson, ScopeVersion, SignatureJson, OutcomeType, OutcomeReason, CancelReason, Version, ServerVersion,
                WorkOrderNumber, CustomerName, CustomerPhone, Address, DeviceName, FaultDescription, ThumbnailUrl)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            row.Id, row.WorkOrderId, row.TechnicianIdsJson, row.Status, row.PlannedStart, row.PlannedEnd, row.StartedAt,
            row.CompletedAt, row.ScopeJson, row.ScopeVersion, row.SignatureJson, row.OutcomeType, row.OutcomeReason,
            row.CancelReason, row.Version, row.ServerVersion, row.WorkOrderNumber, row.CustomerName, row.CustomerPhone, row.Address,
            row.DeviceName, row.FaultDescription, row.ThumbnailUrl);
    }
}
