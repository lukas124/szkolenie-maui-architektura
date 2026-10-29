using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Persistence;
using FieldService.TestDoubles;

namespace FieldService.Infrastructure.Tests.Persistence;

// Dane technika w bazie telefonu: niewysłane zmiany i czyszczenie przed logowaniem innego technika.
[TestFixture]
public class SqliteTechnicianDataTests
{
    private TemporaryAppPaths _paths = null!;
    private SqliteDb _db = null!;
    private SqliteVisitRepository _visits = null!;
    private SqliteTechnicianData _data = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _db = new SqliteDb(_paths);
        _visits = new SqliteVisitRepository(_db);
        _data = new SqliteTechnicianData(_db);
    }

    [TearDown]
    public void TearDown() => _paths.Dispose();

    // Zmiany technika jeszcze nie wychodzą z telefonu – nic nie blokuje logowania innego technika
    [Test]
    public async Task HasUnsentChanges_VisitChangedOnPhone_ReturnsFalseUntilChangesAreSent()
    {
        var visit = TestVisits.InProgress();
        await _visits.AddAsync(visit, CancellationToken.None);
        visit.RecordActivity("DIAG", "Diagnostyka", FixedClock.DefaultNow);
        await _visits.SaveAsync(visit, CancellationToken.None);

        Assert.That(await _data.HasUnsentChangesAsync(CancellationToken.None), Is.False);
    }

    [Test]
    public async Task Clear_RemovesVisitsAndPhotos()
    {
        var visit = TestVisits.InProgress();
        await _visits.AddAsync(visit, CancellationToken.None);
        var photoFile = Path.Combine(_paths.DataDirectory, "zdjecie.jpg");
        await File.WriteAllTextAsync(photoFile, "jpg");
        var photos = new SqliteVisitPhotoStore(_db);
        await photos.AddAsync(visit.Id, photoFile, FixedClock.DefaultNow, CancellationToken.None);
        await new SqliteFlowDraftStore(_db).SaveAsync(new FlowDraft(visit.Id, 2), CancellationToken.None);

        await _data.ClearAsync(CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(await _visits.GetAsync(visit.Id, CancellationToken.None), Is.Null);
            Assert.That(await photos.CountAsync(visit.Id, CancellationToken.None), Is.EqualTo(0));
            Assert.That(File.Exists(photoFile), Is.False);
            Assert.That(await DraftCountAsync(), Is.EqualTo(0));
        });
    }

    private async Task<int> DraftCountAsync()
    {
        var connection = await _db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM flow_drafts");
    }
}
