using System.Globalization;
using System.Text.RegularExpressions;

namespace FieldService.UI.Converters;

// Data wizyty → „dziś”, „wczoraj”, „3 dni temu”, „12.10.2026”. Liczy przy każdym wiązaniu (kultura, formatowanie)
// – koszt w przewijanej liście zmierzymy profilerem w ćwiczeniu 14.
public sealed class RelativeTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset date) return null;

        var polish = new CultureInfo("pl-PL");
        var days = (DateTime.Today - date.LocalDateTime.Date).Days;
        var text = days switch
        {
            0 => "dziś",
            1 => "wczoraj",
            < 7 => string.Format(polish, "{0} dni temu", days),
            _ => date.LocalDateTime.ToString("dd.MM.yyyy", polish)
        };
        // „05.10.2026” → „5.10.2026”: dzień bez zera na początku
        return new Regex(@"^0(\d)\.").Replace(text, "$1.");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
