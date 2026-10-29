using CommunityToolkit.Maui.Views;

namespace FieldService.UI.Controls;

// Pole podpisu klienta (DrawingView z CommunityToolkit.Maui – pakiet tylko w UI). ViewModel dostaje tylko informację,
// czy klient coś narysował (HasDrawing); sam rysunek zostaje w kontrolce.
public sealed class SignaturePad : ContentView
{
    public static readonly BindableProperty HasDrawingProperty =
        BindableProperty.Create(nameof(HasDrawing), typeof(bool), typeof(SignaturePad), false, BindingMode.TwoWay);

    // Tekst przycisku czyszczenia – ustawia strona (napis z zasobów przez {loc:Translate})
    public static readonly BindableProperty ClearTextProperty =
        BindableProperty.Create(nameof(ClearText), typeof(string), typeof(SignaturePad), "",
            propertyChanged: (pad, _, text) => ((SignaturePad)pad)._clear.Text = (string)text);

    private readonly Button _clear;

    private readonly DrawingView _drawing = new()
    {
        HeightRequest = 180,
        LineWidth = 3,
        IsMultiLineModeEnabled = true,
        ShouldClearOnFinish = false
    };

    public SignaturePad()
    {
        _drawing.SetAppThemeColor(DrawingView.LineColorProperty, Colors.Black, Colors.White);
        _drawing.DrawingLineCompleted += (_, _) => HasDrawing = true;

        var clear = _clear = new Button { Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources["SecondaryButton"] };
        clear.Clicked += (_, _) =>
        {
            _drawing.Clear();
            HasDrawing = false;
        };

        Content = new VerticalStackLayout { Spacing = 8, Children = { new Border { Content = _drawing, Style = (Style)Microsoft.Maui.Controls.Application.Current.Resources["Card"] }, clear } };
    }

    public string ClearText
    {
        get => (string)GetValue(ClearTextProperty);
        set => SetValue(ClearTextProperty, value);
    }

    public bool HasDrawing
    {
        get => (bool)GetValue(HasDrawingProperty);
        set => SetValue(HasDrawingProperty, value);
    }
}
