using FieldService.Presentation.Common;
using Microsoft.Extensions.Configuration;

namespace FieldService.UI.Platform;

// Adapter portu IAppEnvironment: środowisko z konfiguracji (appsettings.{Konfiguracja}.json – jak w podstawach),
// wersja z ApplicationDisplayVersion i ApplicationVersion
public sealed class MauiAppEnvironment(IConfiguration configuration) : IAppEnvironment
{
    public string EnvironmentName => configuration["Environment:Name"] ?? "PROD";

    public string VersionText => $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

#if DIAGNOSTICS
    public bool IsDiagnostics => true;
#else
    public bool IsDiagnostics => false;
#endif
}
