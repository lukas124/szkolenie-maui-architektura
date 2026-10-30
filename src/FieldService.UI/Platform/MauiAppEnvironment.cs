using FieldService.Presentation.Common;

namespace FieldService.UI.Platform;

// Adapter portu IAppEnvironment: środowisko z konfiguracji buildu, wersja z ApplicationDisplayVersion i ApplicationVersion
public sealed class MauiAppEnvironment : IAppEnvironment
{
#if DEBUG
    public string EnvironmentName => "DEV";
#elif TEST
    public string EnvironmentName => "TEST";
#else
    public string EnvironmentName => "PROD";
#endif

    public string VersionText => $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

#if DIAGNOSTICS
    public bool IsDiagnostics => true;
#else
    public bool IsDiagnostics => false;
#endif
}
