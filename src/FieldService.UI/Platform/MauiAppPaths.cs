using FieldService.Application.Abstractions;

namespace FieldService.UI.Platform;

// Adapter portu IAppPaths: prywatny katalog danych aplikacji
public sealed class MauiAppPaths : IAppPaths
{
    public string DataDirectory => FileSystem.Current.AppDataDirectory;
}
