using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Outbox;
using SQLite;

namespace FieldService.Infrastructure.Persistence;

// Lokalna replika wizyt w SQLite: repozytorium agregatu (zapis z blokowaniem optymistycznym) i odczyt szczegółów.
// Version – wersja lokalna (dwóch piszących na telefonie), ServerVersion – wersja z serwera (z ostatniego pobrania
// albo z odpowiedzi na wysłaną zmianę). Zmiany wizyty trafiają do outbox w tej samej transakcji (transakcyjna skrzynka nadawcza).
public sealed class SqliteVisitRepository(SqliteDb db, ISyncScheduler sync) : IVisitRepository, IVisitReplica
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
        var connection = await db.GetConnectionAsync();
        await connection.RunInTransactionAsync(c => Insert(c, row, "INSERT"));
    }

    // Jedna transakcja: wiersz wizyty i wiadomość w outbox dla każdego zdarzenia – zapisze się całość albo nic.
    // UPDATE … WHERE Version = wersja odczytana: jeśli w międzyczasie zapisał ktoś inny, nie zmienia się żaden wiersz.
    public async Task<Result> SaveAsync(Visit visit, CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var row = new VisitRow();
        VisitRowMapper.WriteState(visit, row);
        var messages = visit.DomainEvents.Select(OutboxMessage.From).OfType<OutboxMessage>().ToList();
        var saved = false;

        // lambda synchroniczna: transakcja obejmuje tylko to, co wykona się przed jej końcem
        await connection.RunInTransactionAsync(c =>
        {
            var updated = c.Execute(
                """
                UPDATE visits SET Status = ?, StartedAt = ?, CompletedAt = ?, ScopeJson = ?, ScopeVersion = ?,
                    SignatureJson = ?, OutcomeType = ?, OutcomeReason = ?, CancelReason = ?, Version = ?
                WHERE Id = ? AND Version = ?
                """,
                row.Status, row.StartedAt, row.CompletedAt, row.ScopeJson, row.ScopeVersion,
                row.SignatureJson, row.OutcomeType, row.OutcomeReason, row.CancelReason, visit.Version + 1,
                row.Id, visit.Version);
            if (updated == 0) return;

            foreach (var message in messages)
            {
                c.Execute(
                    "INSERT INTO outbox (Id, Type, VisitId, Payload, CreatedAt, Attempts, Failed, TraceParent) VALUES (?, ?, ?, ?, ?, 0, 0, ?)",
                    message.Id, message.Type, message.VisitId, message.Payload, message.CreatedAt, message.TraceParent);
            }
            saved = true;
        });
        if (!saved) return VisitErrors.VersionConflict;

        // zdarzenia obsłużone dopiero po udanym zapisie. Wysyłka nie czeka na zapis – kolejka jest już w bazie
        visit.IncrementVersion();
        visit.ClearDomainEvents();
        sync.RequestSync();
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

    // Wiersz bez lokalnych zmian zastępujemy świeżym stanem z serwera, a wiersz ze zmianami technika (oczekujące
    // wiadomości w outbox) zostaje (INSERT OR IGNORE) – zmiany najpierw muszą dotrzeć na serwer.
    // Starsza odpowiedź (odczyt wysłany przed zmianą, która już dotarła) nie cofa wersji z serwera.
    public async Task SeedAsync(RemoteVisit visit, CancellationToken ct)
    {
        var row = new VisitRow { ServerVersion = visit.Visit.Version };
        VisitRowMapper.WriteState(visit.Visit, row);
        VisitRowMapper.WriteSnapshot(visit.Snapshot, row);

        var connection = await db.GetConnectionAsync();
        await connection.RunInTransactionAsync(c =>
        {
            c.Execute($"DELETE FROM visits WHERE Id = ? AND ServerVersion <= ? AND NOT {HasPendingMessages}",
                row.Id, row.ServerVersion);
            Insert(c, row, "INSERT OR IGNORE");
        });
    }

    public async Task<IReadOnlyDictionary<Guid, VisitStatus>> GetLocallyChangedStatusesAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        var rows = await connection.QueryAsync<VisitRow>($"SELECT Id, Status FROM visits WHERE {HasPendingMessages}");
        return rows.ToDictionary(r => Guid.Parse(r.Id), r => Enum.Parse<VisitStatus>(r.Status));
    }

    // lokalne zmiany wizyty = jej wiadomości czekające na wysłanie
    private const string HasPendingMessages =
        "EXISTS (SELECT 1 FROM outbox WHERE outbox.VisitId = visits.Id AND outbox.SentAt IS NULL AND outbox.Failed = 0)";

    private async Task<VisitRow?> FindAsync(VisitId id)
    {
        var connection = await db.GetConnectionAsync();
        var rows = await connection.QueryAsync<VisitRow>("SELECT * FROM visits WHERE Id = ?", id.Value.ToString());
        return rows.FirstOrDefault();
    }

    private static void Insert(SQLiteConnection connection, VisitRow row, string insert)
    {
        connection.Execute(
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
