using FieldService.Presentation.Common;
using Microsoft.Maui.Controls.Internals;

namespace FieldService.UI.MarkupExtensions;

// Napis strony z zasobów: Text="{loc:Translate Login_Subtitle}" pokazuje tekst spod klucza ze Strings.resx
// (ze Strings.en.resx, gdy telefon jest po angielsku), brak klucza → widać sam klucz.
// Wynik to binding do indeksatora lokalizatora (localizer[Klucz]): gdy lokalizator zgłosi zmianę języka, napis
// zmienia się bez ponownego uruchamiania aplikacji. Wzorzec w StringFormat musi być tekstem – {loc:TranslateFormat}.
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class TranslateExtension : IMarkupExtension<BindingBase>
{
    // Klucz tekstu w Strings.resx
    public string Key { get; set; } = string.Empty;

    // Lokalizator z kontenera: rozszerzenie znaczników tworzy parser XAML, a nie kontener, więc nie da się wstrzyknąć
    // zależności przez konstruktor. Rozszerzenia napisów (to i TranslateFormat) to jedyne takie miejsce i leżą
    // w projekcie korzenia kompozycji (FieldService.UI), który składa kontener.
    // Binding typowany (TypedBinding, jak bindingi kompilowane z x:DataType): bez ścieżki w tekście, więc bez refleksji
    // i bezpieczny przy przycinaniu. Jawne Source – BindingContext strony nie gra tu roli.
    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var localizer = IPlatformApplication.Current!.Services.GetRequiredService<ILocalizer>();
        var key = Key;
        return new TypedBinding<ILocalizer, string>(
            getter: source => (source[key], true),
            setter: null,
            // „Item[]” = zmiana indeksatora, czyli wszystkich kluczy naraz
            handlers: [new(source => source, "Item[]")])
        {
            Source = localizer
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
