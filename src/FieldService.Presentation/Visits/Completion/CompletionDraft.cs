using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;

namespace FieldService.Presentation.Visits.Completion;

// Stan przepływu kreatora wspólny dla kroków. Scoped: jeden na otwarcie kreatora (zakres tworzy NavigationService
// przy otwarciu modala i sprząta przy zamknięciu).
public sealed class CompletionDraft
{
    public VisitId VisitId { get; set; }

    // Szczegóły wizyty po ostatniej zmianie zakresu (odświeżane przez kroki)
    public VisitDetails? Details { get; set; }
}
