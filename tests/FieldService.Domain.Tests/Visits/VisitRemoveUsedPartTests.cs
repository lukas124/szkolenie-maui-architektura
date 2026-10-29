using System;
using System.Linq;
using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using FieldService.Domain.Visits.Events;

namespace FieldService.Domain.Tests.Visits;

// R7: usunięcie omyłkowo dopisanej części z zakresu wizyty.
public class VisitRemoveUsedPartTests
{
    private static readonly DateTimeOffset UsedAt = TestVisits.StartedAt.AddMinutes(10);
    private static readonly DateTimeOffset SignedAt = TestVisits.StartedAt.AddMinutes(20);
    private static readonly DateTimeOffset Now = TestVisits.StartedAt.AddMinutes(30);
    private static readonly PartId Valve = new("P-VALVE-01");

    [Test]
    public void RemoveUsedPart_InProgress_RemovesPartAndRaisesPartRemoved()
    {
        // Sprawdza: w toku część znika z zakresu i powstaje zdarzenie PartRemoved.
        // given
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.ClearDomainEvents();

        // when
        var result = visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(visit.Scope.Parts.Any(p => p.PartId == TestVisits.Filter), Is.False);
        var removed = visit.DomainEvents.OfType<PartRemoved>().Single();
        Assert.That(removed.VisitId, Is.EqualTo(visit.Id));
        Assert.That(removed.PartId, Is.EqualTo(TestVisits.Filter));
        Assert.That(removed.OccurredAt, Is.EqualTo(Now));
    }

    [Test]
    public void RemoveUsedPart_WithoutSignature_DoesNotRaiseSignatureInvalidated()
    {
        // Sprawdza: bez podpisu nie ma czego unieważniać – tylko PartRemoved.
        // given
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.ClearDomainEvents();

        // when
        visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(visit.DomainEvents.OfType<CustomerSignatureInvalidated>(), Is.Empty);
        Assert.That(visit.DomainEvents.OfType<PartRemoved>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void RemoveUsedPart_AfterSignature_InvalidatesSignature()
    {
        // Sprawdza R3: zmiana zakresu po podpisie unieważnia podpis i podbija wersję zakresu.
        // given
        var visit = TestVisits.WithScope();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.SignByCustomer(TestVisits.Signer, SignedAt);
        visit.ClearDomainEvents();
        var scopeVersionBefore = visit.ScopeVersion;
        Assume.That(visit.HasValidSignature(), Is.True);

        // when
        var result = visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(visit.HasValidSignature(), Is.False);
        Assert.That(visit.ScopeVersion, Is.GreaterThan(scopeVersionBefore));
        var invalidated = visit.DomainEvents.OfType<CustomerSignatureInvalidated>().Single();
        Assert.That(invalidated.VisitId, Is.EqualTo(visit.Id));
        Assert.That(invalidated.OccurredAt, Is.EqualTo(Now));
        Assert.That(visit.DomainEvents.OfType<PartRemoved>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void RemoveUsedPart_PartNotInScope_ReturnsPartNotInScope()
    {
        // Sprawdza: części spoza zakresu nie da się usunąć – błąd walidacji.
        // given
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.ClearDomainEvents();

        // when
        var result = visit.RemoveUsedPart(Valve, Now);

        // then
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(VisitErrors.PartNotInScope));
        Assert.That(result.Error!.Code, Is.EqualTo("Visit.PartNotInScope"));
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Validation));
    }

    [Test]
    public void RemoveUsedPart_PartNotInScope_LeavesScopeAndEventsUnchanged()
    {
        // Sprawdza: odmowa niczego nie zmienia – zakres ten sam, brak zdarzeń.
        // given
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(2).Value, UsedAt);
        visit.ClearDomainEvents();

        // when
        visit.RemoveUsedPart(Valve, Now);

        // then
        var part = visit.Scope.Parts.Single();
        Assert.That(part.PartId, Is.EqualTo(TestVisits.Filter));
        Assert.That(part.Quantity.Value, Is.EqualTo(2));
        Assert.That(visit.DomainEvents, Is.Empty);
    }

    [Test]
    public void RemoveUsedPart_EmptyScope_ReturnsPartNotInScope()
    {
        // Sprawdza: na wizycie w toku bez żadnych części każda część jest spoza zakresu.
        // given
        var visit = TestVisits.InProgress();

        // when
        var result = visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(result.Error, Is.EqualTo(VisitErrors.PartNotInScope));
        Assert.That(visit.DomainEvents, Is.Empty);
    }

    [Test]
    public void RemoveUsedPart_Rejected_DoesNotChangeScopeVersionOrSignature()
    {
        // Sprawdza: odmowa na podpisanej wizycie nie podbija wersji zakresu i nie unieważnia podpisu.
        // given
        var visit = TestVisits.WithScope();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.SignByCustomer(TestVisits.Signer, SignedAt);
        visit.ClearDomainEvents();
        var scopeVersionBefore = visit.ScopeVersion;

        // when
        var result = visit.RemoveUsedPart(Valve, Now);

        // then
        Assert.That(result.Error, Is.EqualTo(VisitErrors.PartNotInScope));
        Assert.That(visit.ScopeVersion, Is.EqualTo(scopeVersionBefore));
        Assert.That(visit.HasValidSignature(), Is.True);
        Assert.That(visit.DomainEvents, Is.Empty);
    }

    [Test]
    public void RemoveUsedPart_CompletedVisit_ReturnsNotInProgress()
    {
        // Sprawdza: zakończonej wizyty nie da się zmienić (jak R2).
        // given
        var visit = TestVisits.Completed();
        var scopeVersionBefore = visit.ScopeVersion;

        // when
        var result = visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotInProgress));
        Assert.That(visit.Status, Is.EqualTo(VisitStatus.Completed));
        Assert.That(visit.ScopeVersion, Is.EqualTo(scopeVersionBefore));
        Assert.That(visit.DomainEvents, Is.Empty);
    }

    [Test]
    public void RemoveUsedPart_PlannedVisit_ReturnsNotInProgress()
    {
        // Sprawdza: przed rozpoczęciem wizyty zakresu nie da się zmieniać.
        // given
        var visit = TestVisits.Planned();

        // when
        var result = visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotInProgress));
        Assert.That(visit.Status, Is.EqualTo(VisitStatus.Planned));
        Assert.That(visit.DomainEvents, Is.Empty);
    }

    [Test]
    public void RemoveUsedPart_CancelledVisit_ReturnsNotInProgress()
    {
        // Sprawdza: anulowanej wizyty nie da się zmieniać.
        // given
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.Cancel("Klient odwołał", SignedAt);
        visit.ClearDomainEvents();

        // when
        var result = visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotInProgress));
        Assert.That(visit.Scope.Parts.Single().PartId, Is.EqualTo(TestVisits.Filter));
        Assert.That(visit.DomainEvents, Is.Empty);
    }

    [Test]
    public void RemoveUsedPart_PartUsedTwice_RemovesWholeItem()
    {
        // Sprawdza: usunięcie zdejmuje całą pozycję (wszystkie sztuki), także po dwukrotnym UsePart.
        // given
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.UsePart(TestVisits.Filter, Quantity.Create(2).Value, UsedAt);
        visit.ClearDomainEvents();

        // when
        var result = visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(visit.Scope.Parts, Is.Empty);
        Assert.That(visit.DomainEvents.OfType<PartRemoved>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void RemoveUsedPart_TwoParts_OtherPartStays()
    {
        // Sprawdza: usuwana jest tylko wskazana część, druga zostaje z tą samą ilością.
        // given
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.UsePart(Valve, Quantity.Create(3).Value, UsedAt);
        visit.ClearDomainEvents();

        // when
        visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        var remaining = visit.Scope.Parts.Single();
        Assert.That(remaining.PartId, Is.EqualTo(Valve));
        Assert.That(remaining.Quantity.Value, Is.EqualTo(3));
    }

    [Test]
    public void RemoveUsedPart_WithActivity_KeepsActivities()
    {
        // Sprawdza: usunięcie części nie rusza czynności w zakresie.
        // given
        var visit = TestVisits.WithScope();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.ClearDomainEvents();
        var activitiesBefore = visit.Scope.Activities.ToList();

        // when
        visit.RemoveUsedPart(TestVisits.Filter, Now);

        // then
        Assert.That(visit.Scope.Activities, Is.EqualTo(activitiesBefore));
    }
}

// Wizyty w typowych stanach zbudowane komendami modelu (Domain.Tests nie zna projektu TestDoubles – ten zna Application)
internal static class TestVisits
{
    public const string Signer = "Jan Klient";
    public static readonly PartId Filter = new("P-FILTR-01");
    public static readonly DateTimeOffset StartedAt = new(2026, 10, 29, 8, 30, 0, TimeSpan.FromHours(1));

    public static Visit Planned()
    {
        var visit = Visit.Plan(new VisitId(Guid.NewGuid()), Guid.NewGuid(), ["jan.kowalski@fieldservice.pl"], StartedAt, StartedAt.AddHours(2));
        visit.ClearDomainEvents();
        return visit;
    }

    public static Visit InProgress() => Then(Planned(), v => v.Start(StartedAt));

    public static Visit WithScope() => Then(InProgress(), v => v.RecordActivity("A01", "Diagnostyka pompy ciepła", StartedAt.AddMinutes(5)));

    public static Visit Signed() => Then(WithScope(), v => v.SignByCustomer(Signer, StartedAt.AddMinutes(20)));

    public static Visit Completed() => Then(Signed(), v => v.Complete(VisitOutcome.Fixed, StartedAt.AddMinutes(25)));

    private static Visit Then(Visit visit, Func<Visit, Result> command)
    {
        if (!command(visit).IsSuccess) throw new InvalidOperationException("Nie udało się zbudować wizyty testowej.");
        visit.ClearDomainEvents();
        return visit;
    }
}
