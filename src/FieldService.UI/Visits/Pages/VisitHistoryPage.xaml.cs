using FieldService.UI.Pages;

namespace FieldService.UI.Visits.Pages;

// kompilacja XAML wyłączona – „bo były ostrzeżenia” (odpowiedź LLM)
[XamlCompilation(XamlCompilationOptions.Skip)]
public partial class VisitHistoryPage : BasePage
{
    public VisitHistoryPage() => InitializeComponent();
}
