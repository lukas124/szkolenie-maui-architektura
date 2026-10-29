using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Outbox;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Tests.Persistence;
using FieldService.TestDoubles;
using Moq;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Outbox;

// SyncScheduler: przebieg kolejki na prośbę, tylko z tokenem, jeden przebieg naraz
[TestFixture]
public class SyncSchedulerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(300);

    private TemporaryAppPaths _paths = null!;
    private SqliteDb _db = null!;
    private Mock<IOutboxSender> _sender = null!;
    private AccessTokenStore _tokens = null!;
    private SyncScheduler _scheduler = null!;
    private SqliteVisitRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _db = new SqliteDb(_paths);
        _sender = new Mock<IOutboxSender>();
        _sender
            .Setup(s => s.SendAsync(It.IsAny<OutboxMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SendResults.Ok(10));
        _tokens = new AccessTokenStore();
        _scheduler = new SyncScheduler(() => new OutboxProcessor(_db, _sender.Object), _tokens);
        // repozytorium z atrapą harmonogramu – wiadomości przygotowujemy bez uruchamiania testowanego przebiegu
        _repository = new SqliteVisitRepository(_db, new Mock<ISyncScheduler>().Object);
    }

    [TearDown]
    public void TearDown()
    {
        _paths.Dispose();
    }

    // Zapis wizyty w toku z jedną zmianą → jedna oczekująca wiadomość
    private async Task SavePendingChangeAsync(int minute)
    {
        var id = new VisitId(Guid.NewGuid());
        await _repository.AddAsync(TestVisits.InProgress(id), Ct);
        var visit = TestVisits.InProgress(id);
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, FixedClock.DefaultNow.AddMinutes(minute));
        var result = await _repository.SaveAsync(visit, Ct);
        Assert.That(result.IsSuccess, Is.True, "przygotowanie: zapis wizyty");
    }

    private static async Task<T> WithLimit<T>(Task<T> task)
    {
        var finished = await Task.WhenAny(task, Task.Delay(WaitLimit));
        Assert.That(finished, Is.SameAs(task), "przekroczono limit czasu oczekiwania");
        return await task;
    }

    [Test]
    public async Task RequestSync_NoToken_SendsNothing()
    {
        await SavePendingChangeAsync(1);
        _tokens.AccessToken = null;

        _scheduler.RequestSync();
        await Task.Delay(QuietPeriod);

        _sender.Verify(s => s.SendAsync(It.IsAny<OutboxMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(await _scheduler.GetPendingCountAsync(Ct), Is.EqualTo(1));
    }

    [Test]
    public async Task RequestSync_NoToken_DoesNotRaiseCompleted()
    {
        await SavePendingChangeAsync(1);
        _tokens.AccessToken = null;
        var completed = new TaskCompletionSource<SyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scheduler.Completed += (_, result) => completed.TrySetResult(result);

        _scheduler.RequestSync();
        var finished = await Task.WhenAny(completed.Task, Task.Delay(QuietPeriod));

        Assert.That(finished, Is.Not.SameAs(completed.Task));
    }

    [Test]
    public async Task RequestSync_WithToken_RaisesCompletedWithRunResult()
    {
        await SavePendingChangeAsync(1);
        await SavePendingChangeAsync(2);
        _tokens.AccessToken = "token-technika-123";
        var completed = new TaskCompletionSource<SyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scheduler.Completed += (_, result) => completed.TrySetResult(result);

        _scheduler.RequestSync();
        var result = await WithLimit(completed.Task);

        Assert.That(result.Sent, Is.EqualTo(2));
        Assert.That(result.Pending, Is.EqualTo(0));
        Assert.That(result.Conflicts, Is.Empty);
    }

    [Test]
    public async Task RequestSync_WithTokenAndEmptyQueue_RaisesCompletedWithZeros()
    {
        _tokens.AccessToken = "token-technika-123";
        var completed = new TaskCompletionSource<SyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scheduler.Completed += (_, result) => completed.TrySetResult(result);

        _scheduler.RequestSync();
        var result = await WithLimit(completed.Task);

        Assert.That(result.Sent, Is.EqualTo(0));
        Assert.That(result.Pending, Is.EqualTo(0));
    }

    [Test]
    public async Task RequestSync_SeveralRequestsDuringRun_StartsOneMoreRun()
    {
        await SavePendingChangeAsync(1);
        _tokens.AccessToken = "token-technika-123";
        var senderEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSender = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _sender
            .Setup(s => s.SendAsync(It.IsAny<OutboxMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                senderEntered.TrySetResult(true);
                await releaseSender.Task;
                return SendResults.Ok(10);
            });
        var completedCount = 0;
        var secondCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scheduler.Completed += (_, _) =>
        {
            if (Interlocked.Increment(ref completedCount) == 2)
                secondCompleted.TrySetResult(true);
        };

        _scheduler.RequestSync();
        await WithLimit(senderEntered.Task);
        _scheduler.RequestSync();   // prośby w trakcie przebiegu
        _scheduler.RequestSync();
        releaseSender.SetResult(true);
        await WithLimit(secondCompleted.Task);
        await Task.Delay(QuietPeriod);

        Assert.That(Volatile.Read(ref completedCount), Is.EqualTo(2));
    }

    [Test]
    public async Task GetPendingCountAsync_SavedChanges_ReturnsPendingCount()
    {
        await SavePendingChangeAsync(1);
        await SavePendingChangeAsync(2);

        var count = await _scheduler.GetPendingCountAsync(Ct);

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetPendingCountAsync_AfterRun_ReturnsZero()
    {
        await SavePendingChangeAsync(1);
        _tokens.AccessToken = "token-technika-123";
        var completed = new TaskCompletionSource<SyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scheduler.Completed += (_, result) => completed.TrySetResult(result);
        _scheduler.RequestSync();
        await WithLimit(completed.Task);

        var count = await _scheduler.GetPendingCountAsync(Ct);

        Assert.That(count, Is.EqualTo(0));
    }
}
