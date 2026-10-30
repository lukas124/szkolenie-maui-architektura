namespace FieldService.Application.Abstractions;

// Port: katalogi aplikacji – ścieżki zna platforma
public interface IAppPaths
{
    // dane (baza SQLite, zdjęcia) – zostają
    string DataDirectory { get; }

    // pliki cache (miniatury) – system może je usunąć, gdy brakuje miejsca
    string CacheDirectory { get; }
}
