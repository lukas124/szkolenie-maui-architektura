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
