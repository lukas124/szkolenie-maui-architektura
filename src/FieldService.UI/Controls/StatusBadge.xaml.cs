using FieldService.Presentation.Visits;

namespace FieldService.UI.Controls;

// Własna kontrolka: ContentView + BindableProperty. Typ Tone to semantyczny VisitTone z Presentation – kolor dobiera styl.
public partial class StatusBadge : ContentView
{
    public static readonly BindableProperty ToneProperty =
        BindableProperty.Create(nameof(Tone), typeof(VisitTone), typeof(StatusBadge), VisitTone.Neutral);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(StatusBadge), string.Empty);

    public StatusBadge() => InitializeComponent();

    public VisitTone Tone
    {
        get => (VisitTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
