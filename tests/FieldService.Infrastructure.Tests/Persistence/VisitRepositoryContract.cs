using FieldService.Domain.Visits;
using FieldService.Infrastructure.Persistence;
using FieldService.TestDoubles;
using FieldService.Application.Abstractions;

namespace FieldService.Infrastructure.Tests.Persistence;

// Kontrakt IVisitRepository (LSP): te same testy dla fałszywki i dla SQLite – fałszywka musi zachowywać się jak oryginał.
public abstract class VisitRepositoryContract
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.FromHours(1));

    protected abstract IVisitRepository CreateRepository();

    [Test]
    public async Task Get_UnknownId_ReturnsNull()
    {
        var repository = CreateRepository();

        var visit = await repository.GetAsync(new VisitId(Guid.NewGuid()), CancellationToken.None);

        Assert.That(visit, Is.Null);
    }

    [Test]
    public async Task AddThenGet_ReturnsSameState()
    {
        var repository = CreateRepository();
        var planned = Planned();

        await repository.AddAsync(planned, CancellationToken.None);
        var loaded = await repository.GetAsync(planned.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded?.Id, Is.EqualTo(planned.Id));
            Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.Planned));
            Assert.That(loaded?.Version, Is.EqualTo(planned.Version));
        });
    }

    [Test]
    public async Task ChangeWithoutSave_IsNotVisibleInRepository()
    {
        var repository = CreateRepository();
        var planned = Planned();
        await repository.AddAsync(planned, CancellationToken.None);
        var loaded = (await repository.GetAsync(planned.Id, CancellationToken.None))!;

        loaded.Start(Now);

        var again = await repository.GetAsync(planned.Id, CancellationToken.None);
        Assert.That(again?.Status, Is.EqualTo(VisitStatus.Planned));
    }

    [Test]
    public async Task Save_Success_PersistsStateIncrementsVersionAndClearsEvents()
    {
        var repository = CreateRepository();
        var planned = Planned();
        await repository.AddAsync(planned, CancellationToken.None);
        var visit = (await repository.GetAsync(planned.Id, CancellationToken.None))!;
        var versionBefore = visit.Version;
        visit.Start(Now);

        var result = await repository.SaveAsync(visit, CancellationToken.None);

        var loaded = await repository.GetAsync(planned.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(visit.DomainEvents, Is.Empty);
            Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(loaded?.Version, Is.EqualTo(versionBefore + 1));
        });
    }

    [Test]
    public async Task Save_StaleVersion_ReturnsVersionConflictAndSavesNothing()
    {
        var repository = CreateRepository();
        var planned = Planned();
        await repository.AddAsync(planned, CancellationToken.None);
        var first = (await repository.GetAsync(planned.Id, CancellationToken.None))!;
        var second = (await repository.GetAsync(planned.Id, CancellationToken.None))!;
        first.Start(Now);
        await repository.SaveAsync(first, CancellationToken.None);
        second.Cancel("Decyzja dyspozytora", Now);
        var secondVersion = second.Version;

        var result = await repository.SaveAsync(second, CancellationToken.None);

        var loaded = await repository.GetAsync(planned.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.VersionConflict));
            Assert.That(second.Version, Is.EqualTo(secondVersion));
            Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.InProgress));
        });
    }

    [Test]
    public async Task HasOtherInProgress_OtherVisitInProgress_ReturnsTrue()
    {
        var repository = CreateRepository();
        var started = Planned();
        started.Start(Now);
        await repository.AddAsync(started, CancellationToken.None);
        var planned = Planned();
        await repository.AddAsync(planned, CancellationToken.None);

        Assert.That(await repository.HasOtherInProgressAsync(planned.Id, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task HasOtherInProgress_OnlyThisVisitInProgress_ReturnsFalse()
    {
        var repository = CreateRepository();
        var started = Planned();
        started.Start(Now);
        await repository.AddAsync(started, CancellationToken.None);
        await repository.AddAsync(Planned(), CancellationToken.None);

        Assert.That(await repository.HasOtherInProgressAsync(started.Id, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task HasOtherInProgress_OtherVisitCancelled_ReturnsFalse()
    {
        var repository = CreateRepository();
        var cancelled = Planned();
        cancelled.Cancel("Klient odwołał", Now);
        await repository.AddAsync(cancelled, CancellationToken.None);
        var planned = Planned();
        await repository.AddAsync(planned, CancellationToken.None);

        Assert.That(await repository.HasOtherInProgressAsync(planned.Id, CancellationToken.None), Is.False);
    }

    private static Visit Planned() =>
        Visit.Plan(new VisitId(Guid.NewGuid()), Guid.NewGuid(), ["jan.kowalski@fieldservice.pl"], Now, Now.AddHours(2));
}

[TestFixture]
public class InMemoryVisitRepositoryContractTests : VisitRepositoryContract
{
    protected override IVisitRepository CreateRepository() => new InMemoryVisitRepository();
}

[TestFixture]
public class SqliteVisitRepositoryContractTests : VisitRepositoryContract
{
    private TemporaryAppPaths _paths = null!;

    [SetUp]
    public void SetUp() => _paths = new TemporaryAppPaths();

    [TearDown]
    public void TearDown() => _paths.Dispose();

    protected override IVisitRepository CreateRepository() => new SqliteVisitRepository(new SqliteDb(_paths), Mock.Of<ISyncScheduler>());
}
