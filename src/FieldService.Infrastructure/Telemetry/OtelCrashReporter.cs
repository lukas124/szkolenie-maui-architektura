using System.Diagnostics;
using FieldService.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FieldService.Infrastructure.Telemetry;

// Adapter portu ICrashReporter na OpenTelemetry: wyjątek obsłużony = zdarzenie „exception” na bieżącym spanie (Tempo)
// + log Error (Loki). Awarie zapisane przy poprzednim uruchomieniu wysyła SendPendingAsync jako log Critical z atrybutami
// konwencji OpenTelemetry (exception.type, exception.message, exception.stacktrace) i wersją z chwili awarii.
// flush – wypchnięcie logów z bufora; plik awarii usuwamy dopiero po udanym (bez sieci raport czeka do następnego startu)
public sealed class OtelCrashReporter(PendingCrashStore store, ILogger<OtelCrashReporter> logger, Func<bool>? flush = null)
    : ICrashReporter
{
    public void RecordHandled(Exception exception, IReadOnlyDictionary<string, string>? context = null)
    {
        Activity.Current?.AddException(exception);
        var state = new List<KeyValuePair<string, object?>>();
        if (context is not null) state.AddRange(context.Select(c => new KeyValuePair<string, object?>(c.Key, c.Value)));
        logger.Log(LogLevel.Error, default, state, exception, (_, e) => $"Wyjątek obsłużony: {e?.Message}");
    }

    public Task SendPendingAsync(CancellationToken ct)
    {
        var sent = new List<string>();
        foreach (var (path, crash) in store.ReadAll())
        {
            ct.ThrowIfCancellationRequested();
            List<KeyValuePair<string, object?>> state =
            [
                new("exception.type", crash.Type),
                new("exception.message", crash.Message),
                new("exception.stacktrace", crash.StackTrace),
                new("service.version", crash.AppVersion),
                new("crash.occurred_at", crash.OccurredAt.ToString("O"))
            ];
            logger.Log(LogLevel.Critical, default, state, null, (_, _) => $"Awaria aplikacji: {crash.Type}: {crash.Message}");
            sent.Add(path);
        }
        if (sent.Count > 0 && (flush?.Invoke() ?? true))
            foreach (var path in sent) store.Delete(path);
        return Task.CompletedTask;
    }
}
