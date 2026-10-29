using System.Reflection;
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
        builder.Services
            .AddApplication()
            .AddInfrastructure(api)
            .AddPresentation()
            .AddPlatformServices()
            .AddPages();

#if DEBUG
        builder.Logging.AddDebug(); // okno Output w IDE (z debuggerem)
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true); // Android: adb logcat -s DOTNET
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        return builder.Build();
    }
}
