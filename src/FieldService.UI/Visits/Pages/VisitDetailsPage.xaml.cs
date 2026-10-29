using FieldService.Presentation.Visits;

namespace FieldService.UI.Visits.Pages;

public partial class VisitDetailsPage : ContentPage
{
    public VisitDetailsPage(VisitItem visit)
    {
        InitializeComponent();
        BindingContext = visit;
    }
}
