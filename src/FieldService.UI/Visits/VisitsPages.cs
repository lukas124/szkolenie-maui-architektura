using FieldService.Presentation.Visits;
using FieldService.Presentation.Visits.Completion;
using FieldService.UI.Navigation;
using FieldService.UI.Visits.Pages;

namespace FieldService.UI.Visits;

// Strony modułu Visits w rejestrze stron (ViewModel → strona)
public static class VisitsPages
{
    // TODO: Ćwiczenie 09 – strony modułu w rejestrze: services.AddPage<VisitsListPage, VisitsListViewModel>() i tak samo
    // VisitDetailsPage → VisitDetailsViewModel, CompleteVisitPage → CompleteVisitFlowViewModel
    public static IServiceCollection AddVisitsPages(this IServiceCollection services) => services;
}
