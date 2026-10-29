using System.Globalization;
using FieldService.Presentation.Visits;

namespace FieldService.UI.Converters;

// VisitTone → styl plakietki (Badge.*). Style podaje XAML (pola konwertera), konwerter tylko wybiera.
public sealed class ToneToStyleConverter : IValueConverter
{
    public Style? Neutral { get; set; }
    public Style? Info { get; set; }
    public Style? Success { get; set; }
    public Style? Warning { get; set; }
    public Style? Danger { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        VisitTone.Info => Info,
        VisitTone.Success => Success,
        VisitTone.Warning => Warning,
        VisitTone.Danger => Danger,
        _ => Neutral
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
