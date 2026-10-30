namespace FieldService.Presentation.Common;

// Port prezentacji: środowisko i wersja aplikacji (zna je platforma i konfiguracja buildu)
public interface IAppEnvironment
{
    // DEV (Debug), TEST (Test), PROD (Release)
    string EnvironmentName { get; }

    // wersja widoczna dla użytkownika i numer buildu
    string VersionText { get; }

    // strona Diagnostyka dostępna (Debug i Test)
    bool IsDiagnostics { get; }
}
