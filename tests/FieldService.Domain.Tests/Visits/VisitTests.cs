using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using FieldService.Domain.Visits.Events;

namespace FieldService.Domain.Tests.Visits;

// Testy given–when–then jednostki spójności Visit (reguły R1–R6) – bez atrap.
[TestFixture]
public class VisitTests
{
    private const string Technician = "jan.kowalski@fieldservice.pl";
    private const string SecondTechnician = "anna.nowak@fieldservice.pl";
    private const string Signer = "Jan Klient";

    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly PartId Filter = new("P-FILTR-01");
    private static readonly PartId Valve = new("P-ZAWOR-02");

    // ---------- Plan / Restore ----------

    [Test]
    public void Plan_NewVisit_IsPlannedWithEmptyScopeAndNoEvents()
    {
        var id = new VisitId(Guid.NewGuid());
        var workOrderId = Guid.NewGuid();

        var visit = Visit.Plan(id, workOrderId, [Technician], Now, Now.AddHours(2));

        Assert.Multiple(() =>
        {
            Assert.That(visit.Id, Is.EqualTo(id));
            Assert.That(visit.WorkOrderId, Is.EqualTo(workOrderId));
            Assert.That(visit.TechnicianIds, Is.EqualTo(new[] { Technician }));
            Assert.That(visit.Status, Is.EqualTo(VisitStatus.Planned));
            Assert.That(visit.PlannedStart, Is.EqualTo(Now));
            Assert.That(visit.PlannedEnd, Is.EqualTo(Now.AddHours(2)));
            Assert.That(visit.Scope.IsEmpty, Is.True);
            Assert.That(visit.ScopeVersion, Is.EqualTo(0));
            Assert.That(visit.Signature, Is.Null);
            Assert.That(visit.Outcome, Is.Null);
            Assert.That(visit.StartedAt, Is.Null);
            Assert.That(visit.CompletedAt, Is.Null);
            Assert.That(visit.Version, Is.EqualTo(1));
            Assert.That(visit.DomainEvents, Is.Empty);
        });
    }

    [Test]
    public void Restore_SavedState_RestoresAllFieldsWithoutEvents()
    {
        var id = new VisitId(Guid.NewGuid());
        var workOrderId = Guid.NewGuid();
        var scope = VisitScope.Empty
            .WithActivity(new Activity("A01", "Diagnostyka", Now))
            .WithPart(Filter, Qty(2));
        var signature = CustomerSignature.Restore(Signer, Now.AddMinutes(30), 3);

        var visit = Visit.Restore(id, workOrderId, [Technician, SecondTechnician], VisitStatus.InProgress,
            Now, Now.AddHours(2), Now.AddMinutes(-10), null, scope, 3, signature, null, null, 7);

        Assert.Multiple(() =>
        {
            Assert.That(visit.Id, Is.EqualTo(id));
            Assert.That(visit.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(visit.StartedAt, Is.EqualTo(Now.AddMinutes(-10)));
            Assert.That(visit.Scope.Activities, Is.EqualTo(scope.Activities));
            Assert.That(visit.Scope.Parts, Is.EqualTo(scope.Parts));
            Assert.That(visit.ScopeVersion, Is.EqualTo(3));
            Assert.That(visit.Signature, Is.EqualTo(signature));
            Assert.That(visit.HasValidSignature(), Is.True);
            Assert.That(visit.Version, Is.EqualTo(7));
            Assert.That(visit.DomainEvents, Is.Empty);
        });
    }

    // ---------- R1: Start ----------

    [Test]
    public void Start_FromPlanned_StartsVisitAndRaisesVisitStarted()
    {
        var visit = GivenPlanned();

        var result = visit.Start(Now);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(visit.StartedAt, Is.EqualTo(Now));
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[] { new VisitStarted(visit.Id, Now) }));
        });
    }

    [Test]
    public void Start_WhenInProgress_FailsWithInvalidTransitionAndChangesNothing()
    {
        var visit = GivenInProgress();
        var before = Capture(visit);

        var result = visit.Start(Now.AddHours(1));

        AssertFailedWithoutChange(result, VisitErrors.InvalidTransition, visit, before);
    }

    [Test]
    public void Start_OnCompleted_FailsWithInvalidTransition()
    {
        var visit = GivenCompleted();
        var before = Capture(visit);

        var result = visit.Start(Now.AddHours(2));

        AssertFailedWithoutChange(result, VisitErrors.InvalidTransition, visit, before);
    }

    [Test]
    public void Start_OnCancelled_FailsWithInvalidTransition()
    {
        var visit = GivenCancelled();
        var before = Capture(visit);

        var result = visit.Start(Now.AddHours(1));

        AssertFailedWithoutChange(result, VisitErrors.InvalidTransition, visit, before);
    }

    [Test]
    public void InvalidTransition_IsConflict()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VisitErrors.InvalidTransition.Code, Is.EqualTo("Visit.InvalidTransition"));
            Assert.That(VisitErrors.InvalidTransition.Type, Is.EqualTo(ErrorType.Conflict));
        });
    }

    // ---------- R2: RecordActivity, UsePart ----------

    [Test]
    public void RecordActivity_InProgress_AddsActivityAndRaisesActivityRecorded()
    {
        var visit = GivenInProgress();
        var at = Now.AddMinutes(10);

        var result = visit.RecordActivity("A02", "Wymiana filtra", at);

        var expected = new Activity("A02", "Wymiana filtra", at);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Scope.Activities, Is.EqualTo(new[] { expected }));
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[] { new ActivityRecorded(visit.Id, expected, at) }));
        });
    }

    [Test]
    public void RecordActivity_OnPlanned_FailsWithNotInProgress()
    {
        var visit = GivenPlanned();
        var before = Capture(visit);

        var result = visit.RecordActivity("A02", "Wymiana filtra", Now);

        AssertFailedWithoutChange(result, VisitErrors.NotInProgress, visit, before);
    }

    [Test]
    public void RecordActivity_OnCompleted_FailsWithNotInProgress()
    {
        var visit = GivenCompleted();
        var before = Capture(visit);

        var result = visit.RecordActivity("A02", "Wymiana filtra", Now.AddHours(2));

        AssertFailedWithoutChange(result, VisitErrors.NotInProgress, visit, before);
    }

    [Test]
    public void RecordActivity_EmptyCode_FailsWithActivityCodeRequired()
    {
        var visit = GivenInProgress();
        var before = Capture(visit);

        var result = visit.RecordActivity("", "Wymiana filtra", Now);

        AssertFailedWithoutChange(result, VisitErrors.ActivityCodeRequired, visit, before);
    }

    [Test]
    public void UsePart_InProgress_AddsPartAndRaisesPartUsed()
    {
        var visit = GivenInProgress();
        var at = Now.AddMinutes(15);

        var result = visit.UsePart(Filter, Qty(2), at);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Scope.Parts, Is.EqualTo(new[] { new UsedPart(Filter, Qty(2)) }));
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[] { new PartUsed(visit.Id, Filter, Qty(2), at) }));
        });
    }

    [Test]
    public void UsePart_SamePartTwice_SumsQuantityInOneLine()
    {
        var visit = GivenInProgress();

        visit.UsePart(Filter, Qty(1), Now);
        visit.UsePart(Filter, Qty(2), Now.AddMinutes(1));

        Assert.That(visit.Scope.Parts, Is.EqualTo(new[] { new UsedPart(Filter, Qty(3)) }));
    }

    [Test]
    public void UsePart_OnPlanned_FailsWithNotInProgress()
    {
        var visit = GivenPlanned();
        var before = Capture(visit);

        var result = visit.UsePart(Filter, Qty(1), Now);

        AssertFailedWithoutChange(result, VisitErrors.NotInProgress, visit, before);
    }

    // Karta ćwiczenia 07: część po zakończeniu wizyty jest odrzucana, zakres i zdarzenia bez zmian.
    [Test]
    public void UsePart_OnCompleted_FailsWithNotInProgress()
    {
        var visit = GivenCompleted();
        var before = Capture(visit);

        var result = visit.UsePart(Filter, Qty(1), Now.AddHours(2));

        AssertFailedWithoutChange(result, VisitErrors.NotInProgress, visit, before);
        Assert.That(VisitErrors.NotInProgress.Type, Is.EqualTo(ErrorType.Conflict));
    }

    [Test]
    public void UsePart_OnCancelled_FailsWithNotInProgress()
    {
        var visit = GivenCancelled();
        var before = Capture(visit);

        var result = visit.UsePart(Filter, Qty(1), Now);

        AssertFailedWithoutChange(result, VisitErrors.NotInProgress, visit, before);
    }

    // ---------- R3: zmiana zakresu unieważnia podpis ----------

    // Karta ćwiczenia 07: część dopisana po podpisie unieważnia podpis (ScopeVersion++, zdarzenie po PartUsed).
    [Test]
    public void UsePart_AfterSignature_InvalidatesSignature()
    {
        var visit = GivenSigned();
        var scopeVersionBefore = visit.ScopeVersion;
        var at = Now.AddMinutes(40);

        var result = visit.UsePart(Filter, Qty(1), at);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Signature, Is.Null);
            Assert.That(visit.HasValidSignature(), Is.False);
            Assert.That(visit.ScopeVersion, Is.EqualTo(scopeVersionBefore + 1));
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[]
            {
                new PartUsed(visit.Id, Filter, Qty(1), at),
                new CustomerSignatureInvalidated(visit.Id, at),
            }));
        });
    }

    [Test]
    public void RecordActivity_AfterSignature_InvalidatesSignature()
    {
        var visit = GivenSigned();
        var scopeVersionBefore = visit.ScopeVersion;
        var at = Now.AddMinutes(40);

        var result = visit.RecordActivity("A03", "Odpowietrzenie instalacji", at);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Signature, Is.Null);
            Assert.That(visit.ScopeVersion, Is.EqualTo(scopeVersionBefore + 1));
            Assert.That(visit.DomainEvents.Select(e => e.GetType()),
                Is.EqualTo(new[] { typeof(ActivityRecorded), typeof(CustomerSignatureInvalidated) }));
        });
    }

    [Test]
    public void UsePart_WithoutSignature_IncrementsScopeVersionWithoutInvalidation()
    {
        var visit = GivenInProgress();
        var scopeVersionBefore = visit.ScopeVersion;

        visit.UsePart(Filter, Qty(1), Now);

        Assert.Multiple(() =>
        {
            Assert.That(visit.ScopeVersion, Is.EqualTo(scopeVersionBefore + 1));
            Assert.That(visit.DomainEvents.OfType<CustomerSignatureInvalidated>(), Is.Empty);
        });
    }

    [Test]
    public void RecordActivity_WithoutSignature_IncrementsScopeVersion()
    {
        var visit = GivenInProgress();
        var scopeVersionBefore = visit.ScopeVersion;

        visit.RecordActivity("A01", "Diagnostyka", Now);

        Assert.That(visit.ScopeVersion, Is.EqualTo(scopeVersionBefore + 1));
    }

    [Test]
    public void EachScopeChange_IncrementsScopeVersionByOne()
    {
        var visit = GivenInProgress();
        var scopeVersionBefore = visit.ScopeVersion;

        visit.RecordActivity("A01", "Diagnostyka", Now);
        visit.UsePart(Filter, Qty(1), Now.AddMinutes(1));
        visit.UsePart(Valve, Qty(1), Now.AddMinutes(2));

        Assert.That(visit.ScopeVersion, Is.EqualTo(scopeVersionBefore + 3));
    }

    [Test]
    public void UsePart_AfterSignatureAlreadyInvalidated_DoesNotRaiseSecondInvalidation()
    {
        var visit = GivenSigned();
        visit.UsePart(Filter, Qty(1), Now.AddMinutes(40));
        visit.ClearDomainEvents();

        visit.UsePart(Valve, Qty(1), Now.AddMinutes(41));

        Assert.That(visit.DomainEvents.Select(e => e.GetType()), Is.EqualTo(new[] { typeof(PartUsed) }));
    }

    // Podpis z serwera już nieważny (pod starszą wersją zakresu) – zmiana zakresu nie zgłasza drugi raz unieważnienia
    [Test]
    public void RecordActivity_WithAlreadyInvalidSignatureFromServer_DoesNotRaiseInvalidation()
    {
        var scope = VisitScope.Empty.WithActivity(new Activity("A01", "Diagnostyka", Now.AddMinutes(-20)));
        var visit = Visit.Restore(new VisitId(Guid.NewGuid()), Guid.NewGuid(), [Technician], VisitStatus.InProgress,
            Now, Now.AddHours(2), Now.AddMinutes(-30), null, scope, 2,
            CustomerSignature.Restore(Signer, Now.AddMinutes(-10), 1), null, null, 5);

        var result = visit.RecordActivity("A02", "Czyszczenie", Now);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Signature, Is.Null);
            Assert.That(visit.DomainEvents.OfType<CustomerSignatureInvalidated>(), Is.Empty);
        });
    }

    [Test]
    public void SignByCustomer_AfterScopeChange_SignsNewScopeVersion()
    {
        var visit = GivenSigned();
        visit.UsePart(Filter, Qty(1), Now.AddMinutes(40));

        var result = visit.SignByCustomer(Signer, Now.AddMinutes(45));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Signature?.ScopeVersion, Is.EqualTo(visit.ScopeVersion));
            Assert.That(visit.HasValidSignature(), Is.True);
        });
    }

    // ---------- R4: SignByCustomer ----------

    [Test]
    public void SignByCustomer_WithScope_SignsCurrentScopeVersionAndRaisesCustomerSigned()
    {
        var visit = GivenWithScope();
        var at = Now.AddMinutes(30);

        var result = visit.SignByCustomer(Signer, at);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Signature?.SignerName, Is.EqualTo(Signer));
            Assert.That(visit.Signature?.SignedAt, Is.EqualTo(at));
            Assert.That(visit.Signature?.ScopeVersion, Is.EqualTo(visit.ScopeVersion));
            Assert.That(visit.HasValidSignature(), Is.True);
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[]
            {
                new CustomerSigned(visit.Id, Signer, visit.ScopeVersion, at),
            }));
        });
    }

    [Test]
    public void SignByCustomer_EmptyScope_FailsWithEmptyScope()
    {
        var visit = GivenInProgress();
        var before = Capture(visit);

        var result = visit.SignByCustomer(Signer, Now);

        AssertFailedWithoutChange(result, VisitErrors.EmptyScope, visit, before);
        Assert.That(VisitErrors.EmptyScope.Type, Is.EqualTo(ErrorType.Validation));
    }

    [Test]
    public void SignByCustomer_EmptySigner_FailsWithSignerRequired()
    {
        var visit = GivenWithScope();
        var before = Capture(visit);

        var result = visit.SignByCustomer("", Now);

        AssertFailedWithoutChange(result, VisitErrors.SignerRequired, visit, before);
    }

    // Założenie: same spacje traktujemy jak pusty podpisujący.
    [Test]
    public void SignByCustomer_WhitespaceSigner_FailsWithSignerRequired()
    {
        var visit = GivenWithScope();
        var before = Capture(visit);

        var result = visit.SignByCustomer("   ", Now);

        AssertFailedWithoutChange(result, VisitErrors.SignerRequired, visit, before);
    }

    [Test]
    public void SignByCustomer_OnCompleted_FailsWithNotInProgress()
    {
        var visit = GivenCompleted();
        var before = Capture(visit);

        var result = visit.SignByCustomer(Signer, Now.AddHours(2));

        AssertFailedWithoutChange(result, VisitErrors.NotInProgress, visit, before);
    }

    // ---------- R5: Complete ----------

    // Karta ćwiczenia 07: „naprawione” bez podpisu jest odrzucane błędem walidacji.
    [Test]
    public void Complete_FixedWithoutSignature_FailsWithSignatureRequired()
    {
        var visit = GivenWithScope();
        var before = Capture(visit);

        var result = visit.Complete(VisitOutcome.Fixed, Now.AddHours(1));

        AssertFailedWithoutChange(result, VisitErrors.SignatureRequired, visit, before);
        Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Validation));
    }

    // Karta ćwiczenia 07: „naprawione” z ważnym podpisem kończy wizytę i podnosi VisitCompleted.
    [Test]
    public void Complete_FixedWithValidSignature_RaisesVisitCompleted()
    {
        var visit = GivenSigned();
        var at = Now.AddHours(1);

        var result = visit.Complete(VisitOutcome.Fixed, at);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Status, Is.EqualTo(VisitStatus.Completed));
            Assert.That(visit.Outcome, Is.EqualTo(VisitOutcome.Fixed));
            Assert.That(visit.CompletedAt, Is.EqualTo(at));
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[]
            {
                new VisitCompleted(visit.Id, VisitOutcome.Fixed, at),
            }));
        });
    }

    [Test]
    public void Complete_FixedAfterSignatureInvalidated_FailsWithSignatureRequired()
    {
        var visit = GivenSigned();
        visit.UsePart(Filter, Qty(1), Now.AddMinutes(40));
        visit.ClearDomainEvents();
        var before = Capture(visit);

        var result = visit.Complete(VisitOutcome.Fixed, Now.AddHours(1));

        AssertFailedWithoutChange(result, VisitErrors.SignatureRequired, visit, before);
    }

    [TestCase(VisitOutcomeType.NeedsFollowUp, "Brak części w aucie")]
    [TestCase(VisitOutcomeType.NotFixed, "Urządzenie do wymiany")]
    public void Complete_OutcomeWithReason_WithoutSignature_Completes(VisitOutcomeType type, string reason)
    {
        var visit = GivenWithScope();
        var outcome = VisitOutcome.Create(type, reason).Value;
        var at = Now.AddHours(1);

        var result = visit.Complete(outcome, at);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Status, Is.EqualTo(VisitStatus.Completed));
            Assert.That(visit.Outcome?.Type, Is.EqualTo(type));
            Assert.That(visit.Outcome?.Reason, Is.EqualTo(reason));
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[] { new VisitCompleted(visit.Id, outcome, at) }));
        });
    }

    [Test]
    public void Complete_OnCompleted_FailsWithAlreadyCompleted()
    {
        var visit = GivenCompleted();
        var before = Capture(visit);

        var result = visit.Complete(VisitOutcome.Fixed, Now.AddHours(2));

        AssertFailedWithoutChange(result, VisitErrors.AlreadyCompleted, visit, before);
    }

    [Test]
    public void Complete_OnPlanned_FailsWithInvalidTransition()
    {
        var visit = GivenPlanned();
        var before = Capture(visit);

        var result = visit.Complete(VisitOutcome.Fixed, Now);

        AssertFailedWithoutChange(result, VisitErrors.InvalidTransition, visit, before);
    }

    [Test]
    public void Complete_OnCancelled_FailsWithInvalidTransition()
    {
        var visit = GivenCancelled();
        var before = Capture(visit);

        var result = visit.Complete(VisitOutcome.Fixed, Now);

        AssertFailedWithoutChange(result, VisitErrors.InvalidTransition, visit, before);
    }

    // ---------- R6: Cancel ----------

    [Test]
    public void Cancel_Planned_CancelsAndRaisesVisitCancelled()
    {
        var visit = GivenPlanned();

        var result = visit.Cancel("Klient odwołał", Now);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Status, Is.EqualTo(VisitStatus.Cancelled));
            Assert.That(visit.CancelReason, Is.EqualTo("Klient odwołał"));
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[] { new VisitCancelled(visit.Id, "Klient odwołał", Now) }));
        });
    }

    [Test]
    public void Cancel_InProgress_Cancels()
    {
        var visit = GivenWithScope();

        var result = visit.Cancel("Decyzja dyspozytora", Now.AddMinutes(20));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Status, Is.EqualTo(VisitStatus.Cancelled));
        });
    }

    [Test]
    public void Cancel_OnCompleted_FailsWithAlreadyCompleted()
    {
        var visit = GivenCompleted();
        var before = Capture(visit);

        var result = visit.Cancel("Decyzja dyspozytora", Now.AddHours(2));

        AssertFailedWithoutChange(result, VisitErrors.AlreadyCompleted, visit, before);
        Assert.That(VisitErrors.AlreadyCompleted.Type, Is.EqualTo(ErrorType.Conflict));
    }

    [Test]
    public void Cancel_OnCancelled_FailsWithInvalidTransition()
    {
        var visit = GivenCancelled();
        var before = Capture(visit);

        var result = visit.Cancel("Drugi raz", Now.AddMinutes(5));

        AssertFailedWithoutChange(result, VisitErrors.InvalidTransition, visit, before);
    }

    [Test]
    public void Cancel_EmptyReason_FailsWithReasonRequired()
    {
        var visit = GivenPlanned();
        var before = Capture(visit);

        var result = visit.Cancel("", Now);

        AssertFailedWithoutChange(result, VisitErrors.ReasonRequired, visit, before);
    }

    // ---------- Przebieg całej wizyty ----------

    [Test]
    public void FullVisit_RaisesEventsInOrderOfCommands()
    {
        var visit = GivenPlanned();

        visit.Start(Now);
        visit.RecordActivity("A01", "Diagnostyka", Now.AddMinutes(5));
        visit.UsePart(Filter, Qty(1), Now.AddMinutes(10));
        visit.SignByCustomer(Signer, Now.AddMinutes(20));
        visit.Complete(VisitOutcome.Fixed, Now.AddMinutes(25));

        Assert.That(visit.DomainEvents.Select(e => e.GetType()), Is.EqualTo(new[]
        {
            typeof(VisitStarted), typeof(ActivityRecorded), typeof(PartUsed), typeof(CustomerSigned), typeof(VisitCompleted),
        }));
    }

    [Test]
    public void Commands_DoNotChangeVersion()
    {
        var visit = GivenPlanned();

        visit.Start(Now);
        visit.UsePart(Filter, Qty(1), Now.AddMinutes(10));

        Assert.That(visit.Version, Is.EqualTo(1));
    }

    [Test]
    public void ClearDomainEvents_RemovesEvents()
    {
        var visit = GivenPlanned();
        visit.Start(Now);

        visit.ClearDomainEvents();

        Assert.That(visit.DomainEvents, Is.Empty);
    }

    // ---------- given ----------

    private static Quantity Qty(int value) => Quantity.Create(value).Value;

    private static Visit GivenPlanned(params string[] technicians) =>
        Visit.Plan(new VisitId(Guid.NewGuid()), Guid.NewGuid(),
            technicians.Length == 0 ? new[] { Technician } : technicians, Now, Now.AddHours(2));

    private static Visit GivenInProgress()
    {
        var visit = GivenPlanned();
        Given(visit.Start(Now.AddMinutes(-30)));
        visit.ClearDomainEvents();
        return visit;
    }

    private static Visit GivenWithScope()
    {
        var visit = GivenInProgress();
        Given(visit.RecordActivity("A01", "Diagnostyka", Now.AddMinutes(-25)));
        visit.ClearDomainEvents();
        return visit;
    }

    private static Visit GivenSigned()
    {
        var visit = GivenWithScope();
        Given(visit.SignByCustomer(Signer, Now.AddMinutes(-10)));
        visit.ClearDomainEvents();
        return visit;
    }

    private static Visit GivenCompleted()
    {
        var visit = GivenSigned();
        Given(visit.Complete(VisitOutcome.Fixed, Now.AddMinutes(-5)));
        visit.ClearDomainEvents();
        return visit;
    }

    private static Visit GivenCancelled()
    {
        var visit = GivenPlanned();
        Given(visit.Cancel("Klient odwołał", Now.AddMinutes(-60)));
        visit.ClearDomainEvents();
        return visit;
    }

    private static void Given(Result result) =>
        Assert.That(result.IsSuccess, Is.True, $"Nie udało się przygotować stanu: {result.Error?.Code}");

    // ---------- „odmowa nic nie zmienia” ----------

    private sealed record State(
        VisitStatus Status, int ScopeVersion, CustomerSignature? Signature, VisitOutcome? Outcome,
        DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? CancelReason,
        int Activities, int Parts, int PartsQuantity, int Events, int Version);

    private static State Capture(Visit visit) => new(
        visit.Status, visit.ScopeVersion, visit.Signature, visit.Outcome,
        visit.StartedAt, visit.CompletedAt, visit.CancelReason,
        visit.Scope.Activities.Count, visit.Scope.Parts.Count, visit.Scope.Parts.Sum(p => p.Quantity.Value),
        visit.DomainEvents.Count, visit.Version);

    private static void AssertFailedWithoutChange(Result result, Error expected, Visit visit, State before)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(Capture(visit), Is.EqualTo(before), "Odmowa nie może zmieniać stanu ani zdarzeń");
        });
    }
}
