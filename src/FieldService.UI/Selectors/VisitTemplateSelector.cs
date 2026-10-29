using FieldService.Presentation.Visits;

namespace FieldService.UI.Selectors;

// Wybór szablonu elementu listy: wizyta anulowana wygląda inaczej niż pozostałe
public sealed class VisitTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ActiveTemplate { get; set; }
    public DataTemplate? CancelledTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item is VisitItem { IsCancelled: true } ? CancelledTemplate : ActiveTemplate)
        ?? throw new InvalidOperationException("Ustaw ActiveTemplate i CancelledTemplate w XAML.");
}
