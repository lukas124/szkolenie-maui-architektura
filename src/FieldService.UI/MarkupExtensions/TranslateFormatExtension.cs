using FieldService.Presentation.Common;

namespace FieldService.UI.MarkupExtensions;

// Wzorzec bindingu z zasobów: {Binding Total, StringFormat={loc:TranslateFormat CompleteVisit_LineTotal}} (w zasobach
// tekst z {0}), tak samo TargetNullValue. Te właściwości muszą być tekstem, nie bindingiem, dlatego to rozszerzenie –
// inaczej niż {loc:Translate} – zwraca zwykły string, wstawiony raz przy tworzeniu strony (po zmianie języka telefonu
// wzorzec zmieni się na nowo otwartej stronie).
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class TranslateFormatExtension : IMarkupExtension<string>
{
    // Klucz wzorca w Strings.resx
    public string Key { get; set; } = string.Empty;

    // Lokalizator z kontenera z tego samego powodu co w TranslateExtension (rozszerzenie tworzy parser XAML)
    public string ProvideValue(IServiceProvider serviceProvider) =>
        IPlatformApplication.Current!.Services.GetRequiredService<ILocalizer>()[Key];

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
