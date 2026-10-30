using SQLite;

namespace FieldService.Infrastructure.Persistence;

// Schemat bazy w kolejnych migracjach; numer ostatniej wykonanej w PRAGMA user_version
internal static class SqliteMigrations
{
    private static readonly string[][] Migrations =
    [
        // 1: lokalna replika wizyt (jeden wiersz na wizytę, zakres jako JSON) i zdjęcia wizyt (poza agregatem)
        [
            """
            CREATE TABLE visits (
                Id TEXT PRIMARY KEY,
                WorkOrderId TEXT NOT NULL,
                TechnicianIdsJson TEXT NOT NULL,
                Status TEXT NOT NULL,
                PlannedStart TEXT NOT NULL,
                PlannedEnd TEXT NOT NULL,
                StartedAt TEXT NULL,
                CompletedAt TEXT NULL,
                ScopeJson TEXT NOT NULL,
                ScopeVersion INTEGER NOT NULL,
                SignatureJson TEXT NULL,
                OutcomeType TEXT NULL,
                OutcomeReason TEXT NULL,
                CancelReason TEXT NULL,
                Version INTEGER NOT NULL,
                ServerVersion INTEGER NOT NULL,
                WorkOrderNumber TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                CustomerPhone TEXT NOT NULL,
                Address TEXT NOT NULL,
                DeviceName TEXT NOT NULL,
                FaultDescription TEXT NOT NULL,
                ThumbnailUrl TEXT NOT NULL)
            """,
            """
            CREATE TABLE visit_photos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VisitId TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                TakenAt TEXT NOT NULL)
            """
        ],
        // 2: szkic kreatora zakończenia (jeden wiersz) – przeżywa ubicie procesu przez system
        [
            """
            CREATE TABLE flow_drafts (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                VisitId TEXT NOT NULL,
                StepIndex INTEGER NOT NULL)
            """
        ],
        // 3: skrzynka nadawcza – zmiany wizyt do wysłania na serwer (Id = klucz idempotencji)
        [
            """
            CREATE TABLE outbox (
                Id TEXT PRIMARY KEY,
                Type TEXT NOT NULL,
                VisitId TEXT NOT NULL,
                Payload TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                Attempts INTEGER NOT NULL,
                LastError TEXT NULL,
                SentAt TEXT NULL,
                Failed INTEGER NOT NULL)
            """
        ],
        // 4: migawka ostatniej listy wizyt (jeden wiersz) – lista bez sieci
        [
            """
            CREATE TABLE visit_list_snapshot (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                VisitsJson TEXT NOT NULL,
                SavedAt TEXT NOT NULL)
            """
        ],
        // 5: cache – katalog cen części (offline) i odpowiedzi HTTP z ETagiem (If-None-Match → 304)
        [
            """
            CREATE TABLE parts_catalog (
                PartId TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                UnitPrice TEXT NOT NULL)
            """,
            """
            CREATE TABLE http_cache (
                Url TEXT PRIMARY KEY,
                ETag TEXT NOT NULL,
                Body TEXT NOT NULL,
                StoredAt TEXT NOT NULL)
            """
        ],
        // 6: ślad zapisu zmiany (traceparent) – span wysyłki z outboxu łączy się z nim linkiem
        [
            "ALTER TABLE outbox ADD COLUMN TraceParent TEXT NULL"
        ]
    ];

    public static async Task ApplyAsync(SQLiteAsyncConnection db)
    {
        var current = await db.ExecuteScalarAsync<int>("PRAGMA user_version");
        for (var version = current + 1; version <= Migrations.Length; version++)
        {
            var statements = Migrations[version - 1];
            var target = version;
            await db.RunInTransactionAsync(connection =>
            {
                foreach (var sql in statements) connection.Execute(sql);
                connection.Execute($"PRAGMA user_version = {target}");
            });
        }
    }
}
