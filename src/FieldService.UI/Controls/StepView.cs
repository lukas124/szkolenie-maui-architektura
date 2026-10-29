namespace FieldService.UI.Controls;

// Pokazuje jeden krok kreatora: szablon wybiera selektor (Strategia), treść dostaje krok jako BindingContext
public sealed class StepView : ContentView
{
    public static readonly BindableProperty StepProperty =
        BindableProperty.Create(nameof(Step), typeof(object), typeof(StepView), propertyChanged: (view, _, step) => ((StepView)view).Show(step));

    public object? Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public DataTemplateSelector? TemplateSelector { get; set; }

    private void Show(object? step)
    {
        if (step is null || TemplateSelector is null)
        {
            Content = null;
            return;
        }

        var view = (View)TemplateSelector.SelectTemplate(step, this).CreateContent();
        view.BindingContext = step;
        Content = view;
    }
}
