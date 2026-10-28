using System.Reflection;
using FieldService.UI.Pages;
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

        // Rejestracje w kontenerze. Kolejne pierścienie dojdą tu jako AddApplication(), AddInfrastructure() itd.
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug(); // okno Output w IDE (z debuggerem)
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true); // Android: adb logcat -s DOTNET
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        return builder.Build();
    }
}
