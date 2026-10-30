using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FieldService.Application.Diagnostics;

// Telemetria aplikacji przez API OpenTelemetry wbudowane w .NET – bez pakietów: ActivitySource (ślady i spany) i Meter
// (metryki). Bez SDK (rejestruje je Infrastructure) wywołania nic nie kosztują i nic nie wysyłają.
public static class AppTelemetry
{
    public const string Name = "FieldService";

    public static readonly ActivitySource Source = new(Name);

    public static readonly Meter Meter = new(Name);

    // zakończone wizyty (tag outcome) – zdarzenie domenowe VisitCompleted jako metryka
    public static readonly Counter<long> VisitsCompleted = Meter.CreateCounter<long>("fieldservice.visits.completed");
}
