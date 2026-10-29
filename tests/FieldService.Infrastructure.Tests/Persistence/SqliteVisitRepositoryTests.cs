using FieldService.Application.Visits.Queries;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Persistence;

namespace FieldService.Infrastructure.Tests.Persistence;

// Repozytorium wizyt na prawdziwym SQLite: zapis całości w jednym wierszu, wersja, replika z serwera.
[TestFixture]
public class SqliteVisitRepositoryTests
{
    private const string Technician = "jan.kowalski@fieldservice.pl";
    private const string SecondTechnician = "anna.nowak@fieldservice.pl";
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly PartId Filter = new("P-FILTR-01");
    private static readonly PartId Valve = new("P-ZAWOR-02");

    private TemporaryAppPaths _paths = null!;
    private SqliteVisitRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _repository = new SqliteVisitRepository(new SqliteDb(_paths));
    }

    [TearDown]
    public void TearDown() => _paths.Dispose();

    [Test]
    public async Task Get_UnknownId_ReturnsNull()
    {
        var visit = await _repository.GetAsync(new VisitId(Guid.NewGuid()), CancellationToken.None);

        Assert.That(visit, Is.Null);
    }

    [Test]
    public async Task AddThenGet_ReturnsPlannedVisit()
    {
        var planned = Planned();
        await _repository.AddAsync(planned, CancellationToken.None);

        var loaded = await _repository.GetAsync(planned.Id, CancellationToken.None);

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.WorkOrderId, Is.EqualTo(planned.WorkOrderId));
            Assert.That(loaded.TechnicianIds, Is.EqualTo(planned.TechnicianIds));
            Assert.That(loaded.Status, Is.EqualTo(VisitStatus.Planned));
            Assert.That(loaded.PlannedStart, Is.EqualTo(planned.PlannedStart));
            Assert.That(loaded.PlannedEnd, Is.EqualTo(planned.PlannedEnd));
            Assert.That(loaded.Scope.IsEmpty, Is.True);
            Assert.That(loaded.Version, Is.EqualTo(planned.Version));
        });
    }

    [Test]
    public async Task SaveThenGet_ReturnsWholeStateInProgressWithSignature()
    {
        var visit = await GivenAdded(Planned());
        visit.Start(Now);
        visit.RecordActivity("A01", "Diagnostyka", Now.AddMinutes(5));
        visit.UsePart(Filter, Qty(2), Now.AddMinutes(10));
        visit.UsePart(Valve, Qty(1), Now.AddMinutes(12));
        visit.SignByCustomer("Jan Klient", Now.AddMinutes(20));

        var saved = await _repository.SaveAsync(visit, CancellationToken.None);
        var loaded = await _repository.GetAsync(visit.Id, CancellationToken.None);

        Assert.That(saved.IsSuccess, Is.True);
        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(loaded.StartedAt, Is.EqualTo(Now));
            Assert.That(loaded.Scope.Activities, Is.EqualTo(visit.Scope.Activities));
            Assert.That(loaded.Scope.Parts, Is.EqualTo(visit.Scope.Parts));
            Assert.That(loaded.ScopeVersion, Is.EqualTo(visit.ScopeVersion));
            Assert.That(loaded.Signature, Is.EqualTo(visit.Signature));
            Assert.That(loaded.HasValidSignature(), Is.True);
            Assert.That(loaded.Outcome, Is.Null);
            Assert.That(loaded.Version, Is.EqualTo(visit.Version));
            Assert.That(loaded.DomainEvents, Is.Empty);
        });
    }

    [Test]
    public async Task SaveThenGet_ReturnsCompletedOutcome()
    {
        var visit = await GivenAdded(Planned());
        visit.Start(Now);
        visit.RecordActivity("A01", "Diagnostyka", Now.AddMinutes(5));
        var outcome = VisitOutcome.Create(VisitOutcomeType.NeedsFollowUp, "Brak części w aucie").Value;
        visit.Complete(outcome, Now.AddMinutes(30));

        await _repository.SaveAsync(visit, CancellationToken.None);
        var loaded = await _repository.GetAsync(visit.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.Completed));
            Assert.That(loaded?.Outcome, Is.EqualTo(outcome));
            Assert.That(loaded?.CompletedAt, Is.EqualTo(Now.AddMinutes(30)));
        });
    }

    [Test]
    public async Task SaveThenGet_ReturnsCancelReason()
    {
        var visit = await GivenAdded(Planned());
        visit.Cancel("Klient odwołał", Now);

        await _repository.SaveAsync(visit, CancellationToken.None);
        var loaded = await _repository.GetAsync(visit.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.Cancelled));
            Assert.That(loaded?.CancelReason, Is.EqualTo("Klient odwołał"));
        });
    }

    [Test]
    public async Task Save_Success_IncrementsVersionAndClearsEvents()
    {
        var visit = await GivenAdded(Planned());
        var versionBefore = visit.Version;
        visit.Start(Now);

        var result = await _repository.SaveAsync(visit, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(visit.DomainEvents, Is.Empty);
        });
    }

    // Dwa piszące (UI i synchronizacja) z tej samej wersji: drugi zapis dostaje konflikt i niczego nie nadpisuje.
    [Test]
    public async Task Save_StaleVersion_ReturnsVersionConflictAndKeepsStoredState()
    {
        var planned = await GivenAdded(Planned());
        var first = (await _repository.GetAsync(planned.Id, CancellationToken.None))!;
        var second = (await _repository.GetAsync(planned.Id, CancellationToken.None))!;
        first.Start(Now);
        await _repository.SaveAsync(first, CancellationToken.None);
        second.Cancel("Decyzja dyspozytora", Now);

        var result = await _repository.SaveAsync(second, CancellationToken.None);

        var stored = await _repository.GetAsync(planned.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.VersionConflict));
            Assert.That(stored?.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(stored?.Version, Is.EqualTo(first.Version));
            Assert.That(second.DomainEvents, Is.Not.Empty, "Przy konflikcie zdarzenia zostają na agregacie");
        });
    }

    [Test]
    public async Task GetDetails_UnknownId_ReturnsNull()
    {
        var details = await _repository.GetDetailsAsync(new VisitId(Guid.NewGuid()), CancellationToken.None);

        Assert.That(details, Is.Null);
    }

    [Test]
    public async Task SeedThenGetDetails_ReturnsSnapshotAndVisitState()
    {
        var visit = Planned(Technician, SecondTechnician);
        visit.Start(Now);
        visit.RecordActivity("A01", "Diagnostyka", Now.AddMinutes(5));
        visit.UsePart(Filter, Qty(2), Now.AddMinutes(10));
        visit.SignByCustomer("Jan Klient", Now.AddMinutes(20));
        visit.ClearDomainEvents();
        var snapshot = Snapshot();

        await _repository.SeedAsync(new RemoteVisit(visit, snapshot), CancellationToken.None);
        var details = await _repository.GetDetailsAsync(visit.Id, CancellationToken.None);

        Assert.That(details, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(details!.Id, Is.EqualTo(visit.Id.Value));
            Assert.That(details.WorkOrderNumber, Is.EqualTo(snapshot.WorkOrderNumber));
            Assert.That(details.Customer, Is.EqualTo(snapshot.Customer));
            Assert.That(details.DeviceName, Is.EqualTo(snapshot.DeviceName));
            Assert.That(details.FaultDescription, Is.EqualTo(snapshot.FaultDescription));
            Assert.That(details.ThumbnailUrl, Is.EqualTo(snapshot.ThumbnailUrl));
            Assert.That(details.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(details.PlannedStart, Is.EqualTo(visit.PlannedStart));
            Assert.That(details.PlannedEnd, Is.EqualTo(visit.PlannedEnd));
            Assert.That(details.Activities, Has.Count.EqualTo(1));
            Assert.That(details.Parts, Is.EqualTo(new[] { new VisitPartLine(Filter.Value, 2) }));
            Assert.That(details.HasValidSignature, Is.True);
            Assert.That(details.SignerName, Is.EqualTo("Jan Klient"));
            Assert.That(details.Outcome, Is.Null);
            Assert.That(details.PhotoCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Seed_ThenGet_ReturnsVisitForCommands()
    {
        var visit = Planned();

        await _repository.SeedAsync(new RemoteVisit(visit, Snapshot()), CancellationToken.None);
        var loaded = await _repository.GetAsync(visit.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.Planned));
        });
    }

    // Niewysłane zmiany technika są ważniejsze niż ponowne zasilenie z serwera.
    [Test]
    public async Task Seed_VisitAlreadyInReplica_DoesNotOverwriteLocalChanges()
    {
        var remote = Planned();
        await _repository.SeedAsync(new RemoteVisit(remote, Snapshot()), CancellationToken.None);
        var local = (await _repository.GetAsync(remote.Id, CancellationToken.None))!;
        local.Start(Now);
        await _repository.SaveAsync(local, CancellationToken.None);

        await _repository.SeedAsync(new RemoteVisit(remote, Snapshot()), CancellationToken.None);

        var loaded = await _repository.GetAsync(remote.Id, CancellationToken.None);
        var details = await _repository.GetDetailsAsync(remote.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(loaded?.Version, Is.EqualTo(local.Version));
            Assert.That(details?.Status, Is.EqualTo(VisitStatus.InProgress));
        });
    }

    // Wizyta bez lokalnych zmian dostaje świeży stan z serwera (np. anulowana przez dyspozytora).
    [Test]
    public async Task Seed_VisitWithoutLocalChanges_TakesServerState()
    {
        var remote = Planned();
        await _repository.SeedAsync(new RemoteVisit(remote, Snapshot()), CancellationToken.None);

        var cancelled = Visit.Restore(remote.Id, remote.WorkOrderId, remote.TechnicianIds, VisitStatus.Cancelled,
            remote.PlannedStart, remote.PlannedEnd, null, null, VisitScope.Empty, 0, null, null, "Klient odwołał",
            remote.Version + 1);
        await _repository.SeedAsync(new RemoteVisit(cancelled, Snapshot()), CancellationToken.None);

        var loaded = await _repository.GetAsync(remote.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.Cancelled));
            Assert.That(loaded?.Version, Is.EqualTo(remote.Version + 1));
        });
    }

    [Test]
    public async Task LocallyChangedStatuses_ContainOnlyVisitsChangedOnPhone()
    {
        var changed = Planned();
        var untouched = Planned();
        await _repository.SeedAsync(new RemoteVisit(changed, Snapshot()), CancellationToken.None);
        await _repository.SeedAsync(new RemoteVisit(untouched, Snapshot()), CancellationToken.None);
        var local = (await _repository.GetAsync(changed.Id, CancellationToken.None))!;
        local.Start(Now);
        await _repository.SaveAsync(local, CancellationToken.None);

        var statuses = await _repository.GetLocallyChangedStatusesAsync(CancellationToken.None);

        Assert.That(statuses, Is.EquivalentTo(new Dictionary<Guid, VisitStatus> { [changed.Id.Value] = VisitStatus.InProgress }));
    }

    [Test]
    public async Task Data_SurvivesNewRepositoryInstance()
    {
        var visit = await GivenAdded(Planned());
        visit.Start(Now);
        await _repository.SaveAsync(visit, CancellationToken.None);

        var reopened = new SqliteVisitRepository(new SqliteDb(_paths));
        var loaded = await reopened.GetAsync(visit.Id, CancellationToken.None);

        Assert.That(loaded?.Status, Is.EqualTo(VisitStatus.InProgress));
    }

    // ---------- SqliteVisitPhotoStore ----------

    [Test]
    public async Task PhotoStore_Count_CountsOnlyPhotosOfThatVisit()
    {
        var photos = new SqliteVisitPhotoStore(new SqliteDb(_paths));
        var visitId = new VisitId(Guid.NewGuid());
        await photos.AddAsync(visitId, "/zdjecia/1.jpg", Now, CancellationToken.None);
        await photos.AddAsync(visitId, "/zdjecia/2.jpg", Now.AddMinutes(1), CancellationToken.None);
        await photos.AddAsync(new VisitId(Guid.NewGuid()), "/zdjecia/obca.jpg", Now, CancellationToken.None);

        var count = await photos.CountAsync(visitId, CancellationToken.None);

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task PhotoStore_Count_UnknownVisit_ReturnsZero()
    {
        var photos = new SqliteVisitPhotoStore(new SqliteDb(_paths));

        var count = await photos.CountAsync(new VisitId(Guid.NewGuid()), CancellationToken.None);

        Assert.That(count, Is.EqualTo(0));
    }

    // ---------- given ----------

    private async Task<Visit> GivenAdded(Visit visit)
    {
        await _repository.AddAsync(visit, CancellationToken.None);
        return (await _repository.GetAsync(visit.Id, CancellationToken.None))!;
    }

    private static Visit Planned(params string[] technicians) =>
        Visit.Plan(new VisitId(Guid.NewGuid()), Guid.NewGuid(),
            technicians.Length == 0 ? new[] { Technician } : technicians, Now, Now.AddHours(2));

    private static VisitSnapshot Snapshot() => new(
        "ZL-2026-0042",
        new CustomerSnapshot("Jan Klient", "+48 600 100 200", "ul. Polna 1, Kraków"),
        "Pompa ciepła AW-8", "Nie grzeje wody", "https://example.invalid/thumb.png");

    private static Quantity Qty(int value) => Quantity.Create(value).Value;
}
