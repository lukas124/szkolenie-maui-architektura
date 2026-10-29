using System.Globalization;
using FieldService.Presentation.Visits;

namespace FieldService.UI.Converters;

// VisitTone (Presentation) → kolor z Resources/Styles/Colors.xaml. Decyzja „jak to wygląda” zapada tylko w UI.
public sealed class ToneToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            VisitTone.Info => "InfoColor",
            VisitTone.Success => "SuccessColor",
            VisitTone.Warning => "WarningColor",
            VisitTone.Danger => "DangerColor",
            _ => "NeutralColor"
        };
        return Microsoft.Maui.Controls.Application.Current!.Resources.TryGetValue(key, out var color) ? color : Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
