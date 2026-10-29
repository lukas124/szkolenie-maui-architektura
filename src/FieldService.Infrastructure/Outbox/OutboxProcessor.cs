using System.Globalization;
using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Persistence;

namespace FieldService.Infrastructure.Outbox;

// Jeden przebieg skrzynki nadawczej: wiadomości po kolei, w obrębie wizyty zawsze w kolejności zapisu.
// Wysyłka co najmniej raz – podwójną wysyłkę po zerwanym połączeniu rozpoznaje serwer po kluczu idempotencji.
public sealed class OutboxProcessor(SqliteDb db, IOutboxSender sender)
{
    public const int MaxAttempts = 5;

    private const string Pending = "SentAt IS NULL AND Failed = 0";

    public async Task<SyncResult> ProcessAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        // kolejność zapisu (rowid), nie zegar telefonu – ten może się cofnąć
        var messages = await connection.QueryAsync<OutboxMessage>($"SELECT * FROM outbox WHERE {Pending} ORDER BY rowid");
        var sent = 0;
        var conflicts = new List<VisitId>();
        var rejected = new List<VisitId>();
        var stopped = new HashSet<string>(); // wizyty, których kolejka w tym przebiegu stoi

        // TODO: Ćwiczenie 11 – foreach po messages:
        //   wiadomość wizyty ze stopped → pomiń (kolejność w obrębie wizyty);
        //   serverVersion = await connection.ExecuteScalarAsync<int>("SELECT ServerVersion FROM visits WHERE Id = ?", message.VisitId);
        //   result = await SendAsync(message, serverVersion, ct):
        //   sukces → MarkSentAsync(message, result.Value), sent++;
        //   ErrorType.Network albo ErrorType.Unauthorized → break (kolejka czeka na następny wyzwalacz);
        //   inny błąd → stopped.Add(message.VisitId), a potem:
        //     VisitErrors.VersionConflict → CompensateAsync(message.VisitId, error) i conflicts.Add(new VisitId(Guid.Parse(message.VisitId)));
        //     ostatnia próba (message.Attempts + 1 >= MaxAttempts) → MarkAttemptFailedAsync, CompensateAsync i rejected.Add(…);
        //     każdy inny → MarkAttemptFailedAsync(message, error)

        return new SyncResult(sent, await CountPendingAsync(ct), conflicts, rejected);
    }

    public async Task<int> CountPendingAsync(CancellationToken ct)
    {
        var connection = await db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM outbox WHERE {Pending}");
    }

    // Wysłana: następna wiadomość tej wizyty idzie już z nową wersją z serwera
    private async Task MarkSentAsync(OutboxMessage message, int serverVersion)
    {
        var connection = await db.GetConnectionAsync();
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await connection.RunInTransactionAsync(c =>
        {
            c.Execute("UPDATE outbox SET SentAt = ? WHERE Id = ?", now, message.Id);
            c.Execute("UPDATE visits SET ServerVersion = ? WHERE Id = ?", serverVersion, message.VisitId);
        });
    }

    // Serwer odmówił z innego powodu: próbujemy przy następnym przebiegu
    private async Task MarkAttemptFailedAsync(OutboxMessage message, Error error)
    {
        var connection = await db.GetConnectionAsync();
        await connection.ExecuteAsync("UPDATE outbox SET Attempts = ?, LastError = ? WHERE Id = ?",
            message.Attempts + 1, error.Code, message.Id);
    }

    // Kompensacja: źródło prawdy (serwer) wygrywa. Zmiany technika dla tej wizyty nie pójdą, a lokalną kopię
    // usuwamy – następny odczyt (lista z serwera, szczegóły spoza repliki) pokaże stan z serwera.
    private async Task CompensateAsync(string visitId, Error error)
    {
        var connection = await db.GetConnectionAsync();
        await connection.RunInTransactionAsync(c =>
        {
            c.Execute($"UPDATE outbox SET Failed = 1, LastError = ? WHERE VisitId = ? AND {Pending}", error.Code, visitId);
            c.Execute("DELETE FROM visits WHERE Id = ?", visitId);
        });
    }

    // Wyjątek przy jednej wiadomości (np. nieczytelna odpowiedź) to błąd tej wiadomości, nie koniec kolejki
    private async Task<Result<int>> SendAsync(OutboxMessage message, int serverVersion, CancellationToken ct)
    {
        try
        {
            return await sender.SendAsync(message, serverVersion, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new Error("Outbox.Unexpected", ErrorType.Unexpected);
        }
    }
}
