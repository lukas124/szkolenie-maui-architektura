using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Persistence;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Persistence;

// Szkic kreatora na prawdziwym SQLite (tabela flow_drafts, jeden wiersz)
[TestFixture]
public class SqliteFlowDraftStoreTests
{
    private TemporaryAppPaths _paths = null!;

    [SetUp]
    public void SetUp() => _paths = new TemporaryAppPaths();

    [TearDown]
    public void TearDown() => (_paths as IDisposable)?.Dispose();

    // Nowy magazyn na tym samym katalogu – jak aplikacja uruchomiona po ubiciu procesu
    private SqliteFlowDraftStore CreateStore() => new(new SqliteDb(_paths));

    [Test]
    public async Task GetAsync_NothingSaved_ReturnsNull()
    {
        var draft = await CreateStore().GetAsync(CancellationToken.None);

        Assert.That(draft, Is.Null);
    }

    [Test]
    public async Task SaveAsync_Draft_GetReturnsSameDraft()
    {
        var store = CreateStore();
        var saved = new FlowDraft(new VisitId(Guid.NewGuid()), 2);

        await store.SaveAsync(saved, CancellationToken.None);
        var read = await store.GetAsync(CancellationToken.None);

        Assert.That(read, Is.EqualTo(saved));
    }

    [Test]
    public async Task SaveAsync_SecondDraft_OverwritesFirst()
    {
        // Jeden wiersz – zostaje tylko ostatni szkic
        var store = CreateStore();
        await store.SaveAsync(new FlowDraft(new VisitId(Guid.NewGuid()), 1), CancellationToken.None);
        var latest = new FlowDraft(new VisitId(Guid.NewGuid()), 3);

        await store.SaveAsync(latest, CancellationToken.None);
        var read = await store.GetAsync(CancellationToken.None);

        Assert.That(read, Is.EqualTo(latest));
    }

    [Test]
    public async Task SaveAsync_SameVisitNextStep_OverwritesStepIndex()
    {
        var store = CreateStore();
        var visitId = new VisitId(Guid.NewGuid());
        await store.SaveAsync(new FlowDraft(visitId, 1), CancellationToken.None);

        await store.SaveAsync(new FlowDraft(visitId, 2), CancellationToken.None);
        var read = await store.GetAsync(CancellationToken.None);

        Assert.That(read, Is.EqualTo(new FlowDraft(visitId, 2)));
    }

    [Test]
    public async Task ClearAsync_DraftSaved_GetReturnsNull()
    {
        var store = CreateStore();
        await store.SaveAsync(new FlowDraft(new VisitId(Guid.NewGuid()), 2), CancellationToken.None);

        await store.ClearAsync(CancellationToken.None);
        var read = await store.GetAsync(CancellationToken.None);

        Assert.That(read, Is.Null);
    }

    [Test]
    public async Task ClearAsync_NothingSaved_DoesNotThrow()
    {
        var store = CreateStore();

        Assert.DoesNotThrowAsync(() => store.ClearAsync(CancellationToken.None));
        Assert.That(await store.GetAsync(CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task GetAsync_NewInstanceSameDirectory_ReturnsDraftSavedBefore()
    {
        var saved = new FlowDraft(new VisitId(Guid.NewGuid()), 3);
        await CreateStore().SaveAsync(saved, CancellationToken.None);

        var read = await CreateStore().GetAsync(CancellationToken.None);

        Assert.That(read, Is.EqualTo(saved));
    }

    [Test]
    public async Task GetAsync_NewInstanceAfterClear_ReturnsNull()
    {
        var store = CreateStore();
        await store.SaveAsync(new FlowDraft(new VisitId(Guid.NewGuid()), 1), CancellationToken.None);
        await store.ClearAsync(CancellationToken.None);

        var read = await CreateStore().GetAsync(CancellationToken.None);

        Assert.That(read, Is.Null);
    }
}
