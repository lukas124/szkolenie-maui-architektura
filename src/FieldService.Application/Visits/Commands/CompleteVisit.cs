using System.Diagnostics;
using FieldService.Application.Diagnostics;
using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Commands;

public sealed record CompleteVisit(VisitId VisitId, VisitOutcomeType Outcome, string? Reason);

// Przypadek użycia: handler orkiestruje (wczytaj → model → zapisz), reguła „naprawione wymaga podpisu” jest w Visit
public sealed class CompleteVisitHandler(IVisitRepository visits, IClock clock)
{
    public async Task<Result> HandleAsync(CompleteVisit command, CancellationToken ct)
    {
        // span przypadku użycia: identyfikator wizyty tak, dane klienta i podpis – nigdy
        using var activity = AppTelemetry.Source.StartActivity("CompleteVisit");
        activity?.SetTag("visit.id", command.VisitId.Value.ToString());

        var result = await CompleteAsync(command, ct);
        if (result.IsSuccess)
            AppTelemetry.VisitsCompleted.Add(1, new KeyValuePair<string, object?>("outcome", command.Outcome.ToString()));
        else
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", result.Error!.Type.ToString().ToLowerInvariant());
        }
        return result;
    }

    private async Task<Result> CompleteAsync(CompleteVisit command, CancellationToken ct)
    {
        var visit = await visits.GetAsync(command.VisitId, ct);
        if (visit is null) return VisitErrors.NotFound;

        var outcome = VisitOutcome.Create(command.Outcome, command.Reason);
        if (!outcome.IsSuccess) return outcome.Error!;

        var completed = visit.Complete(outcome.Value, clock.Now);
        if (!completed.IsSuccess) return completed;

        return await visits.SaveAsync(visit, ct);
    }
}
