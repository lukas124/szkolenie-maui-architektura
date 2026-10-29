using FieldService.Application.Visits.Commands;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// Przypadek użycia „Zużyj część”: walidacja ilości, reguły R2/R3 w modelu, zapis tylko przy sukcesie.
[TestFixture]
public class UsePartHandlerTests
{
    private static readonly PartId Filter = TestVisits.Filter;

    private InMemoryVisitRepository _visits = null!;
    private FixedClock _clock = null!;
    private UsePartHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _visits = new InMemoryVisitRepository();
        _clock = new FixedClock();
        _handler = new UsePartHandler(_visits, _clock);
    }

    [Test]
    public async Task UnknownVisit_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(new UsePart(new VisitId(Guid.NewGuid()), Filter, 1), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotFound));
    }

    [TestCase(0)]
    [TestCase(-2)]
    public async Task NonPositiveQuantity_ReturnsInvalidQuantityAndDoesNotSave(int quantity)
    {
        var visit = TestVisits.InProgress();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new UsePart(visit.Id, Filter, quantity), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.InvalidQuantity));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
            Assert.That(_visits.Stored(visit.Id).Scope.Parts, Is.Empty);
        });
    }

    [Test]
    public async Task InProgress_SavesUsedPart()
    {
        var visit = TestVisits.InProgress();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new UsePart(visit.Id, Filter, 2), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_visits.SaveCount, Is.EqualTo(1));
            Assert.That(_visits.Stored(visit.Id).Scope.Parts,
                Is.EqualTo(new[] { new UsedPart(Filter, Quantity.Create(2).Value) }));
        });
    }

    [Test]
    public async Task AfterSignature_SavesInvalidatedSignature()
    {
        var visit = TestVisits.Signed();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new UsePart(visit.Id, Filter, 1), CancellationToken.None);

        var stored = _visits.Stored(visit.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(stored.Signature, Is.Null);
            Assert.That(stored.HasValidSignature(), Is.False);
            Assert.That(stored.ScopeVersion, Is.EqualTo(visit.ScopeVersion + 1));
        });
    }

    [Test]
    public async Task OnPlanned_ReturnsNotInProgressAndDoesNotSave()
    {
        var visit = TestVisits.Planned();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new UsePart(visit.Id, Filter, 1), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.NotInProgress));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task OnCompleted_ReturnsNotInProgressAndDoesNotSave()
    {
        var visit = TestVisits.Completed();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new UsePart(visit.Id, Filter, 1), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.NotInProgress));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }
}
