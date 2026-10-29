using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits.Events;

namespace FieldService.Domain.Visits;

// Wizyta – jednostka spójności modułu Realizacja wizyty (replika na telefonie, ostateczny arbiter: serwer).
// Stan zmienia się tylko przez metody z regułami R1–R7; odmowa = Result z błędem i brak jakiejkolwiek zmiany.
public sealed class Visit : AggregateRoot<VisitId>
{
    private Visit(VisitId id, Guid workOrderId, IReadOnlyList<string> technicianIds, VisitStatus status,
        DateTimeOffset plannedStart, DateTimeOffset plannedEnd, int version) : base(id, version)
    {
        WorkOrderId = workOrderId;
        TechnicianIds = technicianIds;
        Status = status;
        PlannedStart = plannedStart;
        PlannedEnd = plannedEnd;
    }

    public Guid WorkOrderId { get; }                       // inny moduł (Zlecenia) – tylko identyfikator
    public IReadOnlyList<string> TechnicianIds { get; }    // e-maile techników (brygada = dwa)
    public VisitStatus Status { get; private set; }
    public DateTimeOffset PlannedStart { get; }
    public DateTimeOffset PlannedEnd { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public VisitScope Scope { get; private set; } = VisitScope.Empty;
    public int ScopeVersion { get; private set; }
    public CustomerSignature? Signature { get; private set; }
    public VisitOutcome? Outcome { get; private set; }
    public string? CancelReason { get; private set; }

    public static Visit Plan(VisitId id, Guid workOrderId, IReadOnlyList<string> technicianIds,
        DateTimeOffset plannedStart, DateTimeOffset plannedEnd) =>
        new(id, workOrderId, technicianIds, VisitStatus.Planned, plannedStart, plannedEnd, version: 1);

    // Odtworzenie zapisanego stanu (repozytorium, dane z serwera) – bez zdarzeń
    public static Visit Restore(VisitId id, Guid workOrderId, IReadOnlyList<string> technicianIds, VisitStatus status,
        DateTimeOffset plannedStart, DateTimeOffset plannedEnd, DateTimeOffset? startedAt, DateTimeOffset? completedAt,
        VisitScope scope, int scopeVersion, CustomerSignature? signature, VisitOutcome? outcome, string? cancelReason,
        int version) =>
        new(id, workOrderId, technicianIds, status, plannedStart, plannedEnd, version)
        {
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Scope = scope,
            ScopeVersion = scopeVersion,
            Signature = signature,
            Outcome = outcome,
            CancelReason = cancelReason
        };

    // Podpis jest ważny tylko dla wersji zakresu, pod którą klient się podpisał
    public bool HasValidSignature() => Signature is not null && Signature.ScopeVersion == ScopeVersion;

    // R1: rozpocząć można tylko wizytę zaplanowaną
    public Result Start(DateTimeOffset now)
    {
        if (Status != VisitStatus.Planned) return VisitErrors.InvalidTransition;

        Status = VisitStatus.InProgress;
        StartedAt = now;
        Raise(new VisitStarted(Id, now));
        return Result.Success();
    }

    // R2: zakres zmienia się tylko w trakcie wizyty. R3: zmiana zakresu unieważnia podpis
    public Result RecordActivity(string code, string description, DateTimeOffset now)
    {
        if (Status != VisitStatus.InProgress) return VisitErrors.NotInProgress;
        if (string.IsNullOrWhiteSpace(code)) return VisitErrors.ActivityCodeRequired;

        var activity = new Activity(code.Trim(), description.Trim(), now);
        Scope = Scope.WithActivity(activity);
        Raise(new ActivityRecorded(Id, activity, now));
        ScopeChanged(now);
        return Result.Success();
    }

    public Result UsePart(PartId partId, Quantity quantity, DateTimeOffset now)
    {
        if (Status != VisitStatus.InProgress) return VisitErrors.NotInProgress;

        Scope = Scope.WithPart(partId, quantity);
        Raise(new PartUsed(Id, partId, quantity, now));
        ScopeChanged(now);
        return Result.Success();
    }

    // TODO: Ćwiczenie 08 – R7: RemoveUsedPart(PartId partId, DateTimeOffset now) – tylko w trakcie (NotInProgress),
    // część musi być w zakresie (nowy błąd VisitErrors.PartNotInScope), zmiana zakresu jak w UsePart (R3), zdarzenie PartRemoved.
    // Zacznij od testów (docs/ai/prompty/01-test-najpierw.md).

    // R4: klient podpisuje niepusty zakres w trakcie wizyty – podpis zapamiętuje wersję zakresu
    public Result SignByCustomer(string signerName, DateTimeOffset now)
    {
        if (Status != VisitStatus.InProgress) return VisitErrors.NotInProgress;
        if (Scope.IsEmpty) return VisitErrors.EmptyScope;

        var signature = CustomerSignature.Create(signerName, now, ScopeVersion);
        if (!signature.IsSuccess) return signature.Error!;

        Signature = signature.Value;
        Raise(new CustomerSigned(Id, Signature.SignerName, ScopeVersion, now));
        return Result.Success();
    }

    // R5: zakończyć można wizytę w trakcie, a „naprawione” wymaga ważnego podpisu
    public Result Complete(VisitOutcome outcome, DateTimeOffset now)
    {
        if (Status == VisitStatus.Completed) return VisitErrors.AlreadyCompleted;
        if (Status != VisitStatus.InProgress) return VisitErrors.InvalidTransition;
        if (outcome.Type == VisitOutcomeType.Fixed && !HasValidSignature()) return VisitErrors.SignatureRequired;

        Status = VisitStatus.Completed;
        Outcome = outcome;
        CompletedAt = now;
        Raise(new VisitCompleted(Id, outcome, now));
        return Result.Success();
    }

    // R6: anulowanie (decyzja dyspozytora) – zakończonej wizyty nie można anulować
    public Result Cancel(string reason, DateTimeOffset now)
    {
        if (Status == VisitStatus.Completed) return VisitErrors.AlreadyCompleted;
        if (Status == VisitStatus.Cancelled) return VisitErrors.InvalidTransition;
        if (string.IsNullOrWhiteSpace(reason)) return VisitErrors.ReasonRequired;

        Status = VisitStatus.Cancelled;
        CancelReason = reason.Trim();
        Raise(new VisitCancelled(Id, CancelReason, now));
        return Result.Success();
    }

    // R3: każda zmiana zakresu to nowa wersja zakresu, a podpis pod starszą wersją przestaje obowiązywać
    private void ScopeChanged(DateTimeOffset now)
    {
        var wasValid = HasValidSignature();
        ScopeVersion++;
        Signature = null;
        if (wasValid) Raise(new CustomerSignatureInvalidated(Id, now)); // podpis już nieważny (np. z serwera) – bez zdarzenia
    }
}
