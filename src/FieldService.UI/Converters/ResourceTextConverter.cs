using System.Globalization;
using System.Resources;

namespace FieldService.UI.Converters;

// Wartość wyliczenia → tekst z Strings.resx: klucz = parametr + nazwa wartości (null → „All”),
// np. ConverterParameter="Visits_Status_" i VisitStatus.Planned → Visits_Status_Planned
public sealed class ResourceTextConverter : IValueConverter
{
    private static readonly ResourceManager Strings =
        new("FieldService.UI.Resources.Strings.Strings", typeof(ResourceTextConverter).Assembly);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = $"{parameter}{value?.ToString() ?? "All"}";
        return Strings.GetString(key, culture) ?? key;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
