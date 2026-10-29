using FieldService.Presentation.Visits.Completion;

namespace FieldService.UI.Selectors;

// Wybór szablonu kroku kreatora (Strategia). Szablony są polami ustawianymi w XAML – nigdy new DataTemplate
// w OnSelectTemplate (nowy szablon przy każdym wyborze = utrata recyklingu i wydajności).
public sealed class CompletionStepTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Activities { get; set; }
    public DataTemplate? Parts { get; set; }
    public DataTemplate? Signature { get; set; }
    public DataTemplate? Summary { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => item switch
    {
        ActivitiesStepViewModel => Activities!,
        PartsStepViewModel => Parts!,
        SignatureStepViewModel => Signature!,
        SummaryStepViewModel => Summary!,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item.GetType().Name, "Krok bez szablonu.")
    };
}
