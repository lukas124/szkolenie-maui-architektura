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

        foreach (var message in messages)
        {
            if (stopped.Contains(message.VisitId)) continue;

            // wersja, którą widział telefon – serwer odrzuci komendę, jeśli wizyta zmieniła się w międzyczasie
            var serverVersion = await connection.ExecuteScalarAsync<int>("SELECT ServerVersion FROM visits WHERE Id = ?", message.VisitId);
            var result = await SendAsync(message, serverVersion, ct);
            if (result.IsSuccess)
            {
                await MarkSentAsync(message, result.Value);
                sent++;
                continue;
            }

            var error = result.Error!;
            // bez sieci albo bez sesji nic więcej nie wyślemy – kolejka czeka na następny wyzwalacz
            if (error.Type is ErrorType.Network or ErrorType.Unauthorized) break;

            stopped.Add(message.VisitId);
            var visitId = new VisitId(Guid.Parse(message.VisitId));
            if (error == VisitErrors.VersionConflict)
            {
                await CompensateAsync(message.VisitId, error);
                conflicts.Add(visitId);
            }
            else if (message.Attempts + 1 >= MaxAttempts)
            {
                // serwer nie przyjmie tej zmiany – dalsze zmiany wizyty bez niej nie mają sensu: jak przy konflikcie
                await MarkAttemptFailedAsync(message, error);
                await CompensateAsync(message.VisitId, error);
                rejected.Add(visitId);
            }
            else
            {
                await MarkAttemptFailedAsync(message, error);
            }
        }

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
