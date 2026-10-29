using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FieldService.Application.Visits.Commands;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using FieldService.Domain.Visits.Events;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// R7 w przypadku użycia: wczytaj wizytę, usuń część, zapisz.
public class RemoveUsedPartHandlerTests
{
    private static readonly DateTimeOffset UsedAt = TestVisits.StartedAt.AddMinutes(10);
    private static readonly DateTimeOffset Now = TestVisits.StartedAt.AddMinutes(30);

    [Test]
    public async Task UnknownVisit_ReturnsNotFound()
    {
        // Sprawdza: wizyty nie ma w repozytorium → NotFound, bez zapisu.
        // given
        var repository = new InMemoryVisitRepository();
        var handler = new RemoveUsedPartHandler(repository, new FixedClock(Now));
        var unknownId = TestVisits.InProgress().Id;

        // when
        var result = await handler.HandleAsync(new RemoveUsedPart(unknownId, TestVisits.Filter), CancellationToken.None);

        // then
        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotFound));
        Assert.That(repository.SaveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Valid_SavesVisit()
    {
        // Sprawdza: po sukcesie zapisany stan wizyty nie ma usuniętej części, zapis jeden.
        // given
        var repository = new InMemoryVisitRepository();
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(2).Value, UsedAt);
        visit.ClearDomainEvents();
        repository.Add(visit);
        var handler = new RemoveUsedPartHandler(repository, new FixedClock(Now));

        // when
        var result = await handler.HandleAsync(new RemoveUsedPart(visit.Id, TestVisits.Filter), CancellationToken.None);

        // then
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(repository.SaveCount, Is.EqualTo(1));
        Assert.That(repository.Stored(visit.Id).Scope.Parts.Any(p => p.PartId == TestVisits.Filter), Is.False);
    }

    [Test]
    public async Task Valid_SavesPartRemovedEventWithClockTime()
    {
        // Sprawdza: zapis niesie zdarzenie PartRemoved z czasem z zegara.
        // given
        var repository = new InMemoryVisitRepository();
        var visit = TestVisits.InProgress();
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, UsedAt);
        visit.ClearDomainEvents();
        repository.Add(visit);
        var handler = new RemoveUsedPartHandler(repository, new FixedClock(Now));

        // when
        await handler.HandleAsync(new RemoveUsedPart(visit.Id, TestVisits.Filter), CancellationToken.None);

        // then
        var removed = repository.SavedEvents.OfType<PartRemoved>().Single();
        Assert.That(removed.VisitId, Is.EqualTo(visit.Id));
        Assert.That(removed.PartId, Is.EqualTo(TestVisits.Filter));
        Assert.That(removed.OccurredAt, Is.EqualTo(Now));
    }

    [Test]
    public async Task PartNotInScope_ReturnsErrorAndDoesNotSave()
    {
        // Sprawdza: części spoza zakresu → PartNotInScope, nic nie jest zapisywane.
        // given
        var repository = new InMemoryVisitRepository();
        var visit = TestVisits.InProgress();
        repository.Add(visit);
        var handler = new RemoveUsedPartHandler(repository, new FixedClock(Now));

        // when
        var result = await handler.HandleAsync(new RemoveUsedPart(visit.Id, new PartId("P-VALVE-01")), CancellationToken.None);

        // then
        Assert.That(result.Error, Is.EqualTo(VisitErrors.PartNotInScope));
        Assert.That(repository.SaveCount, Is.EqualTo(0));
        Assert.That(repository.SavedEvents, Is.Empty);
    }

    [Test]
    public async Task CompletedVisit_ReturnsNotInProgressAndDoesNotSave()
    {
        // Sprawdza: błąd domeny na zakończonej wizycie wraca do wołającego, bez zapisu.
        // given
        var repository = new InMemoryVisitRepository();
        var visit = TestVisits.Completed();
        repository.Add(visit);
        var handler = new RemoveUsedPartHandler(repository, new FixedClock(Now));

        // when
        var result = await handler.HandleAsync(new RemoveUsedPart(visit.Id, TestVisits.Filter), CancellationToken.None);

        // then
        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotInProgress));
        Assert.That(repository.SaveCount, Is.EqualTo(0));
        Assert.That(repository.Stored(visit.Id).Status, Is.EqualTo(VisitStatus.Completed));
    }
}
