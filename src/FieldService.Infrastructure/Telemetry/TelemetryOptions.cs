namespace FieldService.Infrastructure.Telemetry;

// Ustawienia telemetrii (z appsettings.{Konfiguracja}.json, w Release – adres i nagłówek z sekretu CI)
public sealed class TelemetryOptions
{
    // adres backendu OTLP/HTTP, np. http://10.0.2.2:4318; pusty = bez wysyłki (telemetria nic nie kosztuje)
    public string? OtlpEndpoint { get; init; }

    // nagłówek uwierzytelnienia backendu, np. "Authorization=Basic …"
    public string? OtlpHeaders { get; init; }

    // ułamek śladów wysyłanych do backendu (1.0 = wszystkie)
    public double SamplingRatio { get; init; } = 1.0;

    // service.version – wersja aplikacji z platformy (AppInfo)
    public string AppVersion { get; init; } = "0.0.0";

    // deployment.environment.name – DEV, TEST, PROD (Environment:Name z konfiguracji)
    public string EnvironmentName { get; init; } = "PROD";

    // Debug: spany i logi także w konsoli (adb logcat -s DOTNET)
    public bool ConsoleExporter { get; init; }
}
