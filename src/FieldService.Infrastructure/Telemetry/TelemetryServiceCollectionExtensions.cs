using FieldService.Application.Abstractions;
using FieldService.Application.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FieldService.Infrastructure.Telemetry;

// OpenTelemetry w FieldService: SDK, instrumentacja HttpClient i eksporter OTLP – tylko tutaj (czysty .NET). Application
// zna wyłącznie ActivitySource i Meter z .NET (AppTelemetry), UI rejestruje całość w korzeniu kompozycji.
public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddTelemetry(this IServiceCollection services, TelemetryOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<PendingCrashStore>();
        // plik awarii znika dopiero, gdy log wyszedł z bufora (ForceFlush dostawcy logów)
        services.AddSingleton(sp => new OtelCrashReporter(sp.GetRequiredService<PendingCrashStore>(),
            sp.GetRequiredService<ILogger<OtelCrashReporter>>(), () => FlushLogs(sp)));
        services.AddSingleton<ICrashReporter>(sp => sp.GetRequiredService<OtelCrashReporter>());

        var export = !string.IsNullOrWhiteSpace(options.OtlpEndpoint);
        var resource = ResourceBuilder.CreateDefault()
            .AddService("FieldService.UI", serviceVersion: options.AppVersion)
            .AddAttributes([new("deployment.environment.name", options.EnvironmentName)]);
        var sampling = Math.Clamp(options.SamplingRatio, 0, 1);

        // ślady: nasze spany (AppTelemetry.Source) + żądania HttpClient z nagłówkiem traceparent (W3C)
        services.AddSingleton(_ =>
        {
            var tracing = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resource)
                .AddSource(AppTelemetry.Name)
                .AddHttpClientInstrumentation()
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampling)));
            if (export) tracing.AddOtlpExporter(o => Configure(o, options, "v1/traces"));
            if (options.ConsoleExporter) tracing.AddConsoleExporter();
            return tracing.Build();
        });

        // metryki: nasze liczniki (AppTelemetry.Meter)
        services.AddSingleton(_ =>
        {
            var metrics = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resource)
                .AddMeter(AppTelemetry.Name);
            if (export) metrics.AddOtlpExporter(o => Configure(o, options, "v1/metrics"));
            return metrics.Build();
        });

        // logi: do backendu tylko ostrzeżenia i błędy (transfer i bateria), identyfikatory śladu dochodzą same
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(o =>
            {
                o.SetResourceBuilder(resource);
                o.IncludeFormattedMessage = true;
                if (export) o.AddOtlpExporter(e => Configure(e, options, "v1/logs"));
                if (options.ConsoleExporter) o.AddConsoleExporter();
            });
            logging.AddFilter<OpenTelemetryLoggerProvider>(null, LogLevel.Warning);
        });
        return services;
    }

    // Start po zbudowaniu kontenera: dostawcy śladów i metryk powstają teraz (aplikacja mobilna nie ma hosta, który by
    // ich uruchomił), a awarie z poprzedniego uruchomienia idą do backendu
    // (poza wątkiem startu – błąd konfiguracji telemetrii nie może zamknąć aplikacji, trafia do FireAndForget)
    public static async Task StartTelemetryAsync(this IServiceProvider services, CancellationToken ct)
    {
        await Task.Yield();
        _ = services.GetRequiredService<TracerProvider>();
        _ = services.GetRequiredService<MeterProvider>();
        // bez backendu raporty zostają w telefonie (do pierwszego uruchomienia z adresem OTLP)
        if (!string.IsNullOrWhiteSpace(services.GetRequiredService<TelemetryOptions>().OtlpEndpoint))
            await services.GetRequiredService<OtelCrashReporter>().SendPendingAsync(ct);
    }

    // Aplikacja idzie w tło (system może ją ubić): wysyłamy to, co czeka w buforach – liczniki eksportują się co minutę
    public static void FlushTelemetry(this IServiceProvider services)
    {
        services.GetService<TracerProvider>()?.ForceFlush(2000);
        services.GetService<MeterProvider>()?.ForceFlush(2000);
        FlushLogs(services);
    }

    private static bool FlushLogs(IServiceProvider services) =>
        services.GetService<LoggerProvider>()?.ForceFlush(5000) ?? true;

    // OTLP/HTTP (protobuf): w aplikacji mobilnej HTTP, gRPC zostaje między usługami backendu
    private static void Configure(OtlpExporterOptions otlp, TelemetryOptions options, string signalPath)
    {
        otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
        otlp.Endpoint = new Uri(new Uri(options.OtlpEndpoint!.TrimEnd('/') + "/"), signalPath);
        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders)) otlp.Headers = options.OtlpHeaders;
    }
}
