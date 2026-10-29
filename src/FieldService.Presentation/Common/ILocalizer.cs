using System.Globalization;

namespace FieldService.Presentation.Common;

// Port prezentacji: teksty interfejsu po kluczu oraz kultura i strefa czasowa telefonu do formatowania dat i godzin
// (implementacja w UI: zasoby Strings.resx i Strings.en.resx, ustawienia telefonu)
public interface ILocalizer
{
    // brak klucza w zasobach → sam klucz
    string this[string key] { get; }

    CultureInfo CurrentCulture { get; }

    TimeZoneInfo TimeZone { get; }
}
