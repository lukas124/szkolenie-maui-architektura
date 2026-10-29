namespace FieldService.UI.Controls;

// Własna kontrolka: ContentView + BindableProperty (wersja od LLM – do poprawy w ćwiczeniu 12)
public partial class StatusBadge : ContentView
{
    public static readonly BindableProperty ToneProperty =
        BindableProperty.Create(nameof(Tone), typeof(Color), typeof(StatusBadge), 0);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(StatusBadge), string.Empty);

    public StatusBadge()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public Color Tone
    {
        get => (Color)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
