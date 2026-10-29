using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits.Completion;
using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Presentation.Visits;

// Moduł Visits w pierścieniu prezentacji: ViewModele, kreator zakończenia i trasy modułu
public static class VisitsModule
{
    public static IServiceCollection AddVisitsPresentation(this IServiceCollection services, RouteMap routes)
    {
        services.AddSingleton<IVisitFormatter, VisitFormatter>();
        services.AddSingleton<IVisitItemFactory, VisitItemFactory>();

        services.AddTransient<VisitsListViewModel>();
        services.AddTransient<VisitDetailsViewModel>();
        services.AddTransient<VisitFilterViewModel>();

        // Kreator: stan przepływu jeden na otwarcie (zakres tworzy NavigationService przy otwarciu modala)
        services.AddScoped<CompletionDraft>();
        services.AddTransient<ActivitiesStepViewModel>();
        services.AddTransient<PartsStepViewModel>();
        services.AddTransient<SignatureStepViewModel>();
        services.AddTransient<SummaryStepViewModel>();
        services.AddTransient<CompleteVisitFlowViewModel>();

        routes
            .Map<VisitsListRoute, VisitsListViewModel>()
            .Map<VisitDetailsRoute, VisitDetailsViewModel>()
            .Map<VisitFilterRoute, VisitFilterViewModel>()
            .Map<CompleteVisitRoute, CompleteVisitFlowViewModel>();
        return services;
    }
}
