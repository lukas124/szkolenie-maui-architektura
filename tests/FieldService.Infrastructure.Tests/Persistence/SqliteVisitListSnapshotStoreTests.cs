using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Persistence;
using FieldService.TestDoubles;

namespace FieldService.Infrastructure.Tests.Persistence;

// Migawka listy wizyt na prawdziwym SQLite: zapis, odczyt, nadpisanie, przeżywa nową instancję
[TestFixture]
public class SqliteVisitListSnapshotStoreTests
{
    private TemporaryAppPaths _paths = null!;

    [SetUp]
    public void SetUp() => _paths = new TemporaryAppPaths();

    [TearDown]
    public void TearDown() => _paths.Dispose();

    [Test]
    public async Task Get_NothingSaved_ReturnsNull()
    {
        var store = new SqliteVisitListSnapshotStore(new SqliteDb(_paths));

        Assert.That(await store.GetAsync(CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task SaveThenGetInNewInstance_ReturnsSameVisitsAndTime()
    {
        var visit = Summary("Jan Klient");
        var savedAt = FixedClock.DefaultNow;
        await new SqliteVisitListSnapshotStore(new SqliteDb(_paths)).SaveAsync(new VisitListSnapshot([visit], savedAt), CancellationToken.None);

        var loaded = await new SqliteVisitListSnapshotStore(new SqliteDb(_paths)).GetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Visits, Is.EqualTo(new[] { visit }));
            Assert.That(loaded.SavedAt, Is.EqualTo(savedAt));
        });
    }

    [Test]
    public async Task Save_Twice_KeepsOnlyLatest()
    {
        var store = new SqliteVisitListSnapshotStore(new SqliteDb(_paths));
        await store.SaveAsync(new VisitListSnapshot([Summary("Stara")], FixedClock.DefaultNow), CancellationToken.None);
        await store.SaveAsync(new VisitListSnapshot([Summary("Nowa")], FixedClock.DefaultNow.AddHours(1)), CancellationToken.None);

        var loaded = await store.GetAsync(CancellationToken.None);

        Assert.That(loaded!.Visits.Single().CustomerName, Is.EqualTo("Nowa"));
    }

    private static VisitSummary Summary(string customer) => new(
        Guid.NewGuid(), "ZL-2026-0042", VisitStatus.InProgress, FixedClock.DefaultNow, FixedClock.DefaultNow.AddHours(2),
        customer, "ul. Polna 1, Kraków", "Pompa ciepła AW-8", "Nie grzeje wody", true, "https://example.invalid/thumb.png", 3);
}
