using System.ComponentModel;
using System.Globalization;
using System.Resources;
using FieldService.Presentation.Common;

namespace FieldService.UI.Platform;

// Adapter portu ILocalizer: teksty z Resources/Strings (Strings.resx po polsku, Strings.en.resx po angielsku,
// brak klucza → widać sam klucz). Język telefonu czyta przy starcie i przy powrocie do aplikacji, strefę czasową
// przy każdym użyciu.
public sealed class MauiLocalizer : ILocalizer, INotifyPropertyChanged
{
    private static readonly ResourceManager Strings =
        new("FieldService.UI.Resources.Strings.Strings", typeof(MauiLocalizer).Assembly);

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private CultureInfo _culture = PhoneCulture();

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo CurrentCulture => _culture;

    public TimeZoneInfo TimeZone => TimeZoneInfo.Local;

    public string this[string key] => Strings.GetString(key, _culture) ?? key;

    // Powrót do aplikacji (App.OnResume): technik mógł w tym czasie zmienić język telefonu. Zmiana indeksatora
    // („Item[]” = wszystkie klucze naraz) odświeża napisy z {loc:Translate} bez ponownego uruchamiania aplikacji.
    public void RefreshCulture()
    {
        var culture = PhoneCulture();
        if (culture.Name == _culture.Name) return;
        _culture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    // Kultura telefonu, jeśli aplikacja ma teksty w jej języku. Inny język telefonu → pl-PL (teksty i daty w jednym języku).
    // Android: język z ustawień systemu – .NET czyta go tylko przy starcie procesu, a aktywność obsługuje zmianę języka
    // sama (ConfigChanges.Locale), więc proces trwa dalej. iOS po zmianie języka uruchamia aplikację od nowa.
    private static CultureInfo PhoneCulture()
    {
#if ANDROID
        var locale = Java.Util.Locale.Default;
        var language = locale?.Language;
        var country = locale?.Country;
        if (language is not ("pl" or "en")) return Polish;
        return CultureInfo.GetCultureInfo(string.IsNullOrEmpty(country) ? language : $"{language}-{country}");
#else
        var phone = CultureInfo.CurrentCulture;
        return phone.TwoLetterISOLanguageName is "pl" or "en" ? phone : Polish;
#endif
    }
}
