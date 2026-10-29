using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;

namespace FieldService.TestDoubles;

// Wizyty w typowych stanach, zbudowane przez publiczne komendy modelu (bez zdarzeń – jak po wczytaniu z bazy).
public static class TestVisits
{
    public const string Technician = "jan.kowalski@fieldservice.pl";
    public const string SecondTechnician = "anna.nowak@fieldservice.pl";
    public const string Signer = "Jan Klient";

    public static readonly PartId Filter = new("P-FILTR-01");
    public static readonly DateTimeOffset PlannedStart = FixedClock.DefaultNow;
    public static readonly DateTimeOffset StartedAt = FixedClock.DefaultNow.AddMinutes(-30);

    public static Visit Planned(VisitId? id = null, DateTimeOffset? plannedStart = null, IReadOnlyList<string>? technicians = null)
    {
        var start = plannedStart ?? PlannedStart;
        var visit = Visit.Plan(id ?? new VisitId(Guid.NewGuid()), Guid.NewGuid(), technicians ?? new[] { Technician }, start, start.AddHours(2));
        visit.ClearDomainEvents();
        return visit;
    }

    public static Visit InProgress(VisitId? id = null)
    {
        var visit = Planned(id);
        Ensure(visit.Start(StartedAt));
        visit.ClearDomainEvents();
        return visit;
    }

    // W toku, z niepustym zakresem (jedna czynność), bez podpisu.
    public static Visit WithScope(VisitId? id = null)
    {
        var visit = InProgress(id);
        Ensure(visit.RecordActivity("A01", "Diagnostyka pompy ciepła", StartedAt.AddMinutes(5)));
        visit.ClearDomainEvents();
        return visit;
    }

    // W toku, zakres podpisany przez klienta (podpis ważny).
    public static Visit Signed(VisitId? id = null)
    {
        var visit = WithScope(id);
        Ensure(visit.SignByCustomer(Signer, StartedAt.AddMinutes(20)));
        visit.ClearDomainEvents();
        return visit;
    }

    public static Visit Completed(VisitId? id = null)
    {
        var visit = Signed(id);
        Ensure(visit.Complete(VisitOutcome.Fixed, StartedAt.AddMinutes(25)));
        visit.ClearDomainEvents();
        return visit;
    }

    private static void Ensure(Result result)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Nie udało się zbudować wizyty testowej: {result.Error?.Code}");
        }
    }
}
