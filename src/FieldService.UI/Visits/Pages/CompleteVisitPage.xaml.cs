using FieldService.Presentation.Visits.Completion;
using FieldService.UI.Pages;

namespace FieldService.UI.Visits.Pages;

public partial class CompleteVisitPage : BasePage
{
    public CompleteVisitPage() => InitializeComponent();

    // Strzałka w nagłówku i systemowe „wstecz” = przerwanie kreatora (szkic usunięty, modal zamknięty)
    protected override Task OnBackRequestedAsync() => ((CompleteVisitFlowViewModel)BindingContext).CancelCommand.ExecuteAsync(null);
}
