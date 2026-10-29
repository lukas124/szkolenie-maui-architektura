using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Visits;

// TODO: Ćwiczenie 03 – parametry konstruktora wykorzystasz w TODO, potem usuń tę dyrektywę
#pragma warning disable CS9113
// Fabryka: VisitSummary (model odczytu) → VisitItem (model prezentacji) – teksty z formattera, ton ze statusu
public sealed class VisitItemFactory(IVisitFormatter formatter, ILocalizer localizer) : IVisitItemFactory
{
    public VisitItem Create(VisitSummary visit)
    {
        // TODO: Ćwiczenie 03 – VisitItem z danych wizyty: TimeWindowText i StartsInText (tylko Planned) z formattera,
        // StatusText z localizer[klucz statusu], Tone ze statusu (jak w VisitsListViewModel z 02-clean-architecture:
        // spóźniona → Danger, zaplanowana → Neutral, w trakcie → Info, zakończona → Success, anulowana → Warning)
        throw new NotImplementedException();
    }
}
