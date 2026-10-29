using FieldService.Presentation.Visits;
using FieldService.Presentation.Visits.Completion;
using FieldService.UI.Navigation;
using FieldService.UI.Visits.Pages;

namespace FieldService.UI.Visits;

// Strony modułu Visits w rejestrze stron (ViewModel → strona)
public static class VisitsPages
{
    public static IServiceCollection AddVisitsPages(this IServiceCollection services) => services
        .AddPage<VisitsListPage, VisitsListViewModel>()
        .AddPage<VisitDetailsPage, VisitDetailsViewModel>()
        .AddPage<CompleteVisitPage, CompleteVisitFlowViewModel>();
}
