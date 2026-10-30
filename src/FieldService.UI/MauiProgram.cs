using System.Globalization;
using CommunityToolkit.Maui;
using FieldService.Application;
using FieldService.Infrastructure;
using FieldService.Presentation;
using FieldService.Infrastructure.Telemetry;
using FieldService.UI.Extensions;
using FieldService.UI.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FieldService.UI;

// Korzeń kompozycji (ang. composition root): jedyne miejsce, które składa całą aplikację.
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit() // DrawingView w polu podpisu (SignaturePad)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("IBMPlexSans-Regular.ttf", "PlexRegular");
                fonts.AddFont("IBMPlexSans-SemiBold.ttf", "PlexSemiBold");
                fonts.AddFont("IBMPlexSans-Bold.ttf", "PlexBold");
                fonts.AddFont("JetBrainsMono-Regular.ttf", "MonoRegular");
                fonts.AddFont("JetBrainsMono-Bold.ttf", "MonoBold");
            });

        builder.AddAppConfiguration();

        // Rejestracje pierścieni od środka na zewnątrz – jedyne miejsce, które zna wszystkie projekty
        var api = new ApiOptions
        {
            BaseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Brak Api:BaseUrl w appsettings.json.")
        };
        // Caching:ThumbnailMemoryLimitMb = 0 → miniatury bez limitu (tylko do pomiaru „przed”); brak klucza → 50 MB
        var thumbnailLimitMb = long.TryParse(builder.Configuration["Caching:ThumbnailMemoryLimitMb"], out var mb) ? mb : 50;
        var caching = new CachingOptions
        {
            ThumbnailMemoryLimitBytes = thumbnailLimitMb * 1024 * 1024,
#if DIAGNOSTICS
            CountRequests = true // licznik żądań w Diagnostyce (Debug i Test)
#endif
        };
        builder.Services
            .AddApplication()
            .AddInfrastructure(api, caching)
            .AddTelemetry(TelemetryOptionsFrom(builder.Configuration))
            .AddPresentation()
            .AddPlatformServices()
            .AddPages();

#if DEBUG
        // Kontener sprawdza się przy starcie: brak rejestracji i Scoped pobrany z korzenia (np. CompletionDraft poza
        // zakresem modala) to wyjątek od razu, a nie cicha uwięziona zależność
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
        builder.Logging.AddDebug(); // okno Output w IDE (z debuggerem)
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true); // Android: adb logcat -s DOTNET
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#elif DIAGNOSTICS
        // Test: pomiary w logu (http.requests, visits.list.shown) – adb logcat -s DOTNET
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
#endif

        var app = builder.Build();
        // awarie: zapis przy awarii, wysyłka zapisanych przy starcie (OpenTelemetry)
        CrashHandlers.Register(app.Services);
        app.Services.StartTelemetryAsync(CancellationToken.None).FireAndForget();
        return app;
    }

    // Konfiguracja per środowisko: zasoby osadzone (csproj) – appsettings.json, plik bieżącej konfiguracji
    // (appsettings.Debug/Test/Release.json) i opcjonalny appsettings.Local.json; każdy kolejny nadpisuje poprzednie
    private static MauiAppBuilder AddAppConfiguration(this MauiAppBuilder builder)
    {
        var assembly = typeof(MauiProgram).Assembly;
        foreach (var name in new[] { "appsettings.json", "appsettings.environment.json", "appsettings.local.json" })
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is not null) builder.Configuration.AddJsonStream(stream);
        }
        return builder;
    }

    // Telemetria: adres backendu OTLP z konfiguracji (appsettings.{Konfiguracja}.json), wersja z platformy
    private static TelemetryOptions TelemetryOptionsFrom(IConfiguration configuration) => new()
    {
        OtlpEndpoint = configuration["Telemetry:OtlpEndpoint"],
        OtlpHeaders = configuration["Telemetry:OtlpHeaders"],
        SamplingRatio = double.TryParse(configuration["Telemetry:SamplingRatio"], CultureInfo.InvariantCulture, out var ratio) ? ratio : 1.0,
        AppVersion = AppInfo.Current.VersionString,
        EnvironmentName = configuration["Environment:Name"] ?? "PROD",
#if DEBUG
        ConsoleExporter = true
#endif
    };
}
