namespace FieldService.Application.Abstractions;

// Port: katalog danych aplikacji (baza SQLite, zdjęcia) – ścieżkę zna platforma
public interface IAppPaths
{
    string DataDirectory { get; }
}
