using System.Globalization;
using System.Resources;
using FieldService.Presentation.Common;

namespace FieldService.UI.Platform;

// Adapter portu ILocalizer: teksty z Resources/Strings/Strings.resx (brak klucza → widać sam klucz)
public sealed class MauiLocalizer : ILocalizer
{
    private static readonly ResourceManager Strings =
        new("FieldService.UI.Resources.Strings.Strings", typeof(MauiLocalizer).Assembly);

    public string this[string key] => Strings.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
