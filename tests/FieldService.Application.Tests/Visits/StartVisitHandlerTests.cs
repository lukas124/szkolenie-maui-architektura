using FieldService.Application.Visits;
using FieldService.Application.Visits.Commands;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// Przypadek użycia „Rozpocznij wizytę”: wczytaj → reguła aplikacyjna „jedna wizyta w toku” → model → zapis.
[TestFixture]
public class StartVisitHandlerTests
{
    private InMemoryVisitRepository _visits = null!;
    private FixedClock _clock = null!;
    private StartVisitHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _visits = new InMemoryVisitRepository();
        _clock = new FixedClock();
        _handler = new StartVisitHandler(_visits, _clock);
    }

    [Test]
    public async Task UnknownVisit_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(new StartVisit(new VisitId(Guid.NewGuid())), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.NotFound));
            Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PlannedVisit_StartsWithClockTimeAndSaves()
    {
        var visit = TestVisits.Planned();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new StartVisit(visit.Id), CancellationToken.None);

        var stored = _visits.Stored(visit.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_visits.SaveCount, Is.EqualTo(1));
            Assert.That(stored.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(stored.StartedAt, Is.EqualTo(_clock.Now));
            Assert.That(stored.Version, Is.EqualTo(visit.Version + 1));
        });
    }

    // Reguła aplikacyjna: inna wizyta w toku → Conflict, bez zapisu.
    [Test]
    public async Task OtherVisitInProgress_ReturnsAnotherInProgressAndDoesNotSave()
    {
        _visits.Add(TestVisits.InProgress());
        var visit = TestVisits.Planned();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new StartVisit(visit.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitApplicationErrors.AnotherInProgress));
            Assert.That(result.Error?.Code, Is.EqualTo("Visit.AnotherInProgress"));
            Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Conflict));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
            Assert.That(_visits.Stored(visit.Id).Status, Is.EqualTo(VisitStatus.Planned));
        });
    }

    [Test]
    public async Task OtherVisitOnlyPlanned_StartsVisit()
    {
        _visits.Add(TestVisits.Planned());
        var visit = TestVisits.Planned();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new StartVisit(visit.Id), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task AlreadyStarted_ReturnsInvalidTransitionAndDoesNotSave()
    {
        var visit = TestVisits.InProgress();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new StartVisit(visit.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.InvalidTransition));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task SaveConflict_ReturnsVersionConflict()
    {
        var visit = TestVisits.Planned();
        var repository = new Mock<IVisitRepository>();
        repository.Setup(r => r.GetAsync(visit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(visit);
        repository.Setup(r => r.SaveAsync(visit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(VisitErrors.VersionConflict));
        var handler = new StartVisitHandler(repository.Object, _clock);

        var result = await handler.HandleAsync(new StartVisit(visit.Id), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(VisitErrors.VersionConflict));
    }
}
