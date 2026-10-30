using System.Globalization;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Tests;

// Atrapa ILocalizer: teksty jak w Strings.resx (tylko te, których potrzebują testy), kultura pl-PL,
// strefa czasowa Europe/Warsaw
public sealed class TestLocalizer : ILocalizer
{
    private readonly Dictionary<string, string> _texts = new()
    {
        ["Format_Time"] = "H:mm",
        ["Format_Day"] = "ddd dd.MM",
        ["Format_DayTime"] = "d.MM H:mm",
        ["Format_Date"] = "d.MM.yyyy",
        ["Visits_Today"] = "dziś",
        ["Visits_Tomorrow"] = "jutro",
        ["Visits_StartsIn"] = "za {0} min",
        ["Visits_Summary"] = "Zakończone: {0} z {1} · do zrobienia: {2}",
        ["Visits_Empty"] = "Brak wizyt na dziś",
        ["Visits_LastUpdated"] = "Aktualizacja: {0}",
        ["Visits_OfflineSince"] = "Bez sieci – dane z {0}",
        ["Visits_Status_Planned"] = "Zaplanowana",
        ["Visits_Status_Late"] = "Spóźniona",
        ["Visits_Status_InProgress"] = "W trakcie",
        ["Visits_Status_Completed"] = "Zakończona",
        ["Visits_Status_Cancelled"] = "Anulowana",
        ["Error_Network"] = "Brak połączenia z serwerem.",
        ["Error_Unexpected"] = "Nieoczekiwany błąd.",
        ["Error_Auth.InvalidCredentials"] = "Nieprawidłowy e-mail lub PIN."
    };

    public string this[string key] => _texts.GetValueOrDefault(key, key);

    public CultureInfo CurrentCulture { get; } = CultureInfo.GetCultureInfo("pl-PL");

    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
}
