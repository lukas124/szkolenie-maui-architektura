using FieldService.Application.Visits.Commands;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Domain.Visits.Events;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// Przypadek użycia „Zakończ wizytę”: handler orkiestruje, agregat decyduje; przy odmowie brak zapisu.
[TestFixture]
[Ignore("Ćwiczenie 07 (krok 5): usuń Ignore po uzupełnieniu CompleteVisitHandler")]
public class CompleteVisitHandlerTests
{
    private InMemoryVisitRepository _visits = null!;
    private FixedClock _clock = null!;
    private CompleteVisitHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _visits = new InMemoryVisitRepository();
        _clock = new FixedClock();
        _handler = new CompleteVisitHandler(_visits, _clock);
    }

    // Karta Bloku 07.
    [Test]
    public async Task UnknownVisit_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(
            new CompleteVisit(new VisitId(Guid.NewGuid()), VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.NotFound));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }

    // Karta Bloku 07: „naprawione” bez podpisu → Validation i brak zapisu.
    [Test]
    public async Task WithoutSignature_ReturnsValidationAndDoesNotSave()
    {
        var visit = TestVisits.WithScope();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(
            new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.SignatureRequired));
            Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Validation));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
            Assert.That(_visits.Stored(visit.Id).Status, Is.EqualTo(VisitStatus.InProgress));
        });
    }

    // Karta Bloku 07: poprawne zakończenie zapisuje wizytę (stan + zdarzenie VisitCompleted).
    [Test]
    public async Task Valid_SavesVisit()
    {
        var visit = TestVisits.Signed();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(
            new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);

        var stored = _visits.Stored(visit.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_visits.SaveCount, Is.EqualTo(1));
            Assert.That(stored.Status, Is.EqualTo(VisitStatus.Completed));
            Assert.That(stored.Outcome, Is.EqualTo(VisitOutcome.Fixed));
            Assert.That(stored.CompletedAt, Is.EqualTo(_clock.Now));
            Assert.That(_visits.SavedEvents, Is.EqualTo(new IDomainEvent[]
            {
                new VisitCompleted(visit.Id, VisitOutcome.Fixed, _clock.Now),
            }));
        });
    }

    [Test]
    public async Task NeedsFollowUpWithReason_SavesWithoutSignature()
    {
        var visit = TestVisits.WithScope();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(
            new CompleteVisit(visit.Id, VisitOutcomeType.NeedsFollowUp, "Brak części w aucie"), CancellationToken.None);

        var stored = _visits.Stored(visit.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(stored.Outcome?.Type, Is.EqualTo(VisitOutcomeType.NeedsFollowUp));
            Assert.That(stored.Outcome?.Reason, Is.EqualTo("Brak części w aucie"));
        });
    }

    [TestCase(VisitOutcomeType.NeedsFollowUp)]
    [TestCase(VisitOutcomeType.NotFixed)]
    public async Task OutcomeWithoutReason_ReturnsReasonRequiredAndDoesNotSave(VisitOutcomeType type)
    {
        var visit = TestVisits.Signed();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new CompleteVisit(visit.Id, type, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.ReasonRequired));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task AlreadyCompleted_ReturnsAlreadyCompletedAndDoesNotSave()
    {
        var visit = TestVisits.Completed();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(
            new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.AlreadyCompleted));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task SaveConflict_ReturnsVersionConflict()
    {
        var visit = TestVisits.Signed();
        var repository = new Mock<IVisitRepository>();
        repository.Setup(r => r.GetAsync(visit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(visit);
        repository.Setup(r => r.SaveAsync(visit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(VisitErrors.VersionConflict));
        var handler = new CompleteVisitHandler(repository.Object, _clock);

        var result = await handler.HandleAsync(
            new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.VersionConflict));
            repository.Verify(r => r.SaveAsync(visit, It.IsAny<CancellationToken>()), Times.Once);
        });
    }
}
