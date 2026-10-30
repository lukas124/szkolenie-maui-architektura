using System.Globalization;

namespace FieldService.UI.Converters;

// Miniatura z cache (VisitItem.Thumbnail – funkcja otwierająca strumień) → ImageSource. ViewModel nie zna typów MAUI,
// a obrazek pobiera IThumbnailCache (pamięć z limitem → plik → sieć), nie wbudowany cache UriImageSource.
public sealed class ThumbnailConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Func<CancellationToken, Task<Stream>> open ? ImageSource.FromStream(open) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
