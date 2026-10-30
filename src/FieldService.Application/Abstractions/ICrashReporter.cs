namespace FieldService.Application.Abstractions;

// Port: wyjątek obsłużony (aplikacja działa dalej) – do raportu awarii. Jak wysłać (OpenTelemetry, inna usługa) –
// decyduje adapter w Infrastructure.
public interface ICrashReporter
{
    void RecordHandled(Exception exception, IReadOnlyDictionary<string, string>? context = null);
}
