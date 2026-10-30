using FieldService.Application.Abstractions;

namespace FieldService.UI.Platform;

// Adapter portu IAppPaths: prywatne katalogi aplikacji – dane i cache (ten system może czyścić, gdy brakuje miejsca)
public sealed class MauiAppPaths : IAppPaths
{
    public string DataDirectory => FileSystem.Current.AppDataDirectory;

    public string CacheDirectory => FileSystem.Current.CacheDirectory;
}
