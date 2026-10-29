using System.Globalization;
using FieldService.Presentation.Visits;

namespace FieldService.UI.Converters;

// VisitTone (Presentation) → kolor z Resources/Styles/Colors.xaml. Decyzja „jak to wygląda” zapada tylko w UI.
public sealed class ToneToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // TODO: Ćwiczenie 02 – VisitTone → klucz koloru z Colors.xaml (Info → "InfoColor", Success → "SuccessColor",
        // Warning → "WarningColor", Danger → "DangerColor", pozostałe → "NeutralColor"), potem
        // Microsoft.Maui.Controls.Application.Current!.Resources.TryGetValue(klucz, out var color)
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
