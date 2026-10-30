using System.Globalization;
using System.Text.Json;
using FieldService.Application.Abstractions;

namespace FieldService.Infrastructure.Telemetry;

// Awaria nieobsłużona – zapisana w chwili awarii, wysłana przy następnym starcie
public sealed record PendingCrash(string Type, string Message, string StackTrace, string AppVersion, DateTimeOffset OccurredAt);

// Crash zabija proces, zanim eksporter OpenTelemetry cokolwiek wyśle – dlatego mały plik JSON od razu, synchronicznie,
// w katalogu danych aplikacji; wysyłka i usunięcie przy następnym starcie (OtelCrashReporter.SendPendingAsync).
public sealed class PendingCrashStore(IAppPaths paths, TelemetryOptions options)
{
    private string Directory => Path.Combine(paths.DataDirectory, "crashes");

    // nigdy nie rzuca – wyjątek w obsłudze awarii zasłoniłby właściwą przyczynę
    public void Save(Exception exception)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var now = DateTimeOffset.UtcNow;
            var crash = new PendingCrash(exception.GetType().FullName ?? exception.GetType().Name, exception.Message,
                exception.ToString(), options.AppVersion, now);
            var name = $"{now.UtcTicks.ToString(CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}.json";
            File.WriteAllText(Path.Combine(Directory, name), JsonSerializer.Serialize(crash));
        }
        catch (Exception)
        {
        }
    }

    // od najstarszej; plik, którego nie da się odczytać, jest usuwany (nie zablokuje kolejnych wysyłek)
    public IReadOnlyList<(string Path, PendingCrash Crash)> ReadAll()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];

        var crashes = new List<(string, PendingCrash)>();
        foreach (var file in System.IO.Directory.GetFiles(Directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                if (JsonSerializer.Deserialize<PendingCrash>(File.ReadAllText(file)) is { } crash) crashes.Add((file, crash));
                else Delete(file);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Delete(file);
            }
        }
        return crashes;
    }

    public void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
