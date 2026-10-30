using System.Reflection;
using CommunityToolkit.Maui;
using FieldService.Application;
using FieldService.Infrastructure;
using FieldService.Presentation;
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

        // appsettings.json jest zasobem wbudowanym w zestaw (EmbeddedResource w FieldService.UI.csproj)
        using var settings = Assembly.GetExecutingAssembly().GetManifestResourceStream("appsettings.json");
        if (settings is not null)
            builder.Configuration.AddJsonStream(settings);

        // Rejestracje pierścieni od środka na zewnątrz – jedyne miejsce, które zna wszystkie projekty
        var api = new ApiOptions
        {
            BaseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Brak Api:BaseUrl w appsettings.json.")
        };
        var caching = new CachingOptions
        {
#if DIAGNOSTICS
            CountRequests = true // licznik żądań w Diagnostyce (Debug i Test)
#endif
        };
        builder.Services
            .AddApplication()
            .AddInfrastructure(api, caching)
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

        return builder.Build();
    }
}
