using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Commands;

public sealed record CompleteVisit(VisitId VisitId, VisitOutcomeType Outcome, string? Reason);

// Przypadek użycia: handler orkiestruje (wczytaj → model → zapisz), reguła „naprawione wymaga podpisu” jest w Visit
// TODO: Ćwiczenie 07 – parametry konstruktora wykorzystasz w TODO, potem usuń tę dyrektywę
#pragma warning disable CS9113
public sealed class CompleteVisitHandler(IVisitRepository visits, IClock clock)
{
    public Task<Result> HandleAsync(CompleteVisit command, CancellationToken ct)
    {
        // TODO: Ćwiczenie 07 – wczytaj wizytę (brak → VisitErrors.NotFound), VisitOutcome.Create(command.Outcome,
        // command.Reason), visit.Complete(…, clock.Now); przy błędzie zwróć go BEZ zapisu, inaczej visits.SaveAsync
        throw new NotImplementedException();
    }
}
