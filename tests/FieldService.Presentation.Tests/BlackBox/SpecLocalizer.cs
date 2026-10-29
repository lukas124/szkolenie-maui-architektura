using System.Globalization;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>
/// Atrapa lokalizatora z tekstami z kontraktu checkpointu 03 (kultura pl-PL, strefa czasowa Europe/Warsaw).
/// Brak klucza → zwraca sam klucz (tak jak lokalizator w aplikacji).
/// </summary>
internal sealed class SpecLocalizer : ILocalizer
{
    public const string Today = "dziś";
    public const string Tomorrow = "jutro";
    public const string StartsIn = "za {0} min";
    public const string Summary = "Zakończone: {0} z {1} · do zrobienia: {2}";
    public const string Empty = "Brak wizyt na dziś";
    public const string LastUpdated = "Aktualizacja: {0}";

    // Teksty statusów, tytułów i błędów nie są podane w kontrakcie – przyjęte wartości testowe.
    public const string StatusPlanned = "Zaplanowana";
    public const string StatusLate = "Spóźniona";
    public const string StatusInProgress = "W trakcie";
    public const string StatusCompleted = "Zakończona";
    public const string StatusCancelled = "Anulowana";
    public const string ErrorTitle = "Błąd";
    public const string Ok = "OK";
    public const string Yes = "Tak";
    public const string No = "Nie";
    public const string ErrorNetwork = "Brak połączenia z internetem.";
    public const string ErrorUnauthorized = "Sesja wygasła. Zaloguj się ponownie.";
    public const string ErrorUnexpected = "Coś poszło nie tak.";
    public const string ErrorInvalidCredentials = "Nieprawidłowy e-mail lub PIN.";

    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal)
    {
        ["Format_Time"] = "H:mm",
        ["Format_Day"] = "ddd dd.MM",
        ["Visits_Today"] = Today,
        ["Visits_Tomorrow"] = Tomorrow,
        ["Visits_StartsIn"] = StartsIn,
        ["Visits_Summary"] = Summary,
        ["Visits_Empty"] = Empty,
        ["Visits_LastUpdated"] = LastUpdated,
        ["Visits_Status_Planned"] = StatusPlanned,
        ["Visits_Status_Late"] = StatusLate,
        ["Visits_Status_InProgress"] = StatusInProgress,
        ["Visits_Status_Completed"] = StatusCompleted,
        ["Visits_Status_Cancelled"] = StatusCancelled,
        ["Error_Title"] = ErrorTitle,
        ["Common_Ok"] = Ok,
        ["Common_Yes"] = Yes,
        ["Common_No"] = No,
        ["Error_Network"] = ErrorNetwork,
        ["Error_Unauthorized"] = ErrorUnauthorized,
        ["Error_Unexpected"] = ErrorUnexpected,
        ["Error_Auth.InvalidCredentials"] = ErrorInvalidCredentials,
    };

    public SpecLocalizer(IReadOnlyDictionary<string, string>? extra = null)
    {
        if (extra is null)
        {
            return;
        }

        foreach (var (key, value) in extra)
        {
            _texts[key] = value;
        }
    }

    public string this[string key] => _texts.TryGetValue(key, out var text) ? text : key;

    public CultureInfo CurrentCulture { get; } = CultureInfo.GetCultureInfo("pl-PL");

    public TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
}
