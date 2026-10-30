using System.Diagnostics.Metrics;
using FieldService.Application.Diagnostics;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;

namespace FieldService.Infrastructure.Outbox;

// Kiedy wysyłać: prośby z wyzwalaczy (zapis, logowanie, powrót z tła, zmiana sieci, „Wyślij teraz”).
// Jeden przebieg naraz. Prośby w trakcie przebiegu dają jeden kolejny przebieg. Wiadomości czekają w bazie, więc
// przebieg przerwany przez system (ubicie procesu) niczego nie gubi.
// Fabryka zamiast procesora: singleton trzymałby na zawsze HttpClient bramy (uwięziona zależność) – procesor
// powstaje na każdy przebieg.
public sealed class SyncScheduler(Func<OutboxProcessor> processors, AccessTokenStore tokens, ICrashReporter crashes) : ISyncScheduler
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _queued; // 1 = przebieg czeka na swoją kolej

    // metryka: wiadomości czekające na wysłanie po ostatnim przebiegu (jeden harmonogram na aplikację)
    private static int _lastPending;
    private static readonly ObservableGauge<int> PendingGauge =
        AppTelemetry.Meter.CreateObservableGauge("fieldservice.outbox.pending", () => _lastPending);

    public event EventHandler<SyncResult>? Completed;

    public void RequestSync()
    {
        // przebieg już czeka na swoją kolej – ta prośba do niego dołącza
        if (Interlocked.Exchange(ref _queued, 1) == 1) return;
        // poza wątkiem wywołującego (często UI): przebieg to SQLite i HTTP, nikt na niego nie czeka
        _ = Task.Run(RunAsync);
    }

    public Task<int> GetPendingCountAsync(CancellationToken ct) => processors().CountPendingAsync(ct);

    private async Task RunAsync()
    {
        await _gate.WaitAsync();
        try
        {
            Interlocked.Exchange(ref _queued, 0);
            // bez sesji serwer i tak odmówi (401) – wiadomości poczekają na zalogowanie
            if (tokens.AccessToken is null) return;

            var result = await processors().ProcessAsync(CancellationToken.None);
            _lastPending = result.Pending;
            Completed?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            // nieudany przebieg nie może zamknąć aplikacji, wiadomości zostają w bazie do następnego wyzwalacza –
            // ale zostaje ślad w raporcie awarii
            crashes.RecordHandled(ex, new Dictionary<string, string> { ["operation"] = "outbox.process" });
        }
        finally
        {
            _gate.Release();
        }
    }
}
