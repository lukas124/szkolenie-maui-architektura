using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Outbox;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Tests.Persistence;
using FieldService.TestDoubles;
using Moq;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Outbox;

// Tworzenie wyników Result<int> w jednym miejscu (używane też w SyncSchedulerTests)
internal static class SendResults
{
    public static Result<int> Ok(int version) => Result<int>.Success(version);
    public static Result<int> Fail(Error error) => Result<int>.Failure(error);

    public static readonly Error Network = new("Visits.Network", ErrorType.Network);
    public static readonly Error Unauthorized = new("Visits.Unauthorized", ErrorType.Unauthorized);
    public static readonly Error Unexpected = new("Visits.Unexpected", ErrorType.Unexpected);
}

// Outbox na prawdziwym SQLite: SqliteVisitRepository (transakcja + wiadomości) i OutboxProcessor (wysyłka kolejki)
[TestFixture]
public class OutboxTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset T0 = FixedClock.DefaultNow;

    private TemporaryAppPaths _paths = null!;
    private SqliteDb _db = null!;
    private Mock<ISyncScheduler> _sync = null!;
    private Mock<IOutboxSender> _sender = null!;
    private SqliteVisitRepository _repository = null!;
    private OutboxProcessor _processor = null!;

    // wywołania bramy: (Id wiadomości, Type, VisitId, expectedVersion)
    private List<(string Id, string Type, string VisitId, int ExpectedVersion)> _calls = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _db = new SqliteDb(_paths);
        _sync = new Mock<ISyncScheduler>();
        _sender = new Mock<IOutboxSender>();
        _repository = new SqliteVisitRepository(_db, _sync.Object);
        _processor = new OutboxProcessor(_db, _sender.Object);
        _calls = [];
    }

    [TearDown]
    public void TearDown()
    {
        _paths.Dispose();
    }

    // ---------- pomocnicze ----------

    private static DateTimeOffset At(int minutes) => T0.AddMinutes(minutes);

    private static VisitId NewId() => new(Guid.NewGuid());

    private static string Key(VisitId id) => id.Value.ToString("D");

    private static RemoteVisit Remote(Visit visit) =>
        new(visit, new VisitSnapshot("WO-2026-001", new CustomerSnapshot("Jan Kowalski", "500600700", "ul. Polna 1"),
            "Kocioł gazowy", "Brak ciepłej wody", "https://api.test/thumb.png"));

    // Wizyta w toku zapisana w replice; zwraca świeżą kopię o tej samej wersji (do zmiany i SaveAsync)
    private async Task<Visit> AddInProgressAsync(VisitId id)
    {
        await _repository.AddAsync(TestVisits.InProgress(id), Ct);
        return TestVisits.InProgress(id);
    }

    // Wizyta w toku z jedną zapisaną zmianą (PartUsed) – jedna oczekująca wiadomość
    private async Task SaveOnePartAsync(VisitId id, int minute)
    {
        var visit = await AddInProgressAsync(id);
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(minute));
        var result = await _repository.SaveAsync(visit, Ct);
        Assert.That(result.IsSuccess, Is.True, "przygotowanie: zapis wizyty");
    }

    // Wizyta w toku z dwiema zmianami w jednym zapisie: PartUsed, potem ActivityRecorded
    private async Task SavePartAndActivityAsync(VisitId id, int firstMinute)
    {
        var visit = await AddInProgressAsync(id);
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(firstMinute));
        visit.RecordActivity("DIAG", "Diagnostyka", At(firstMinute + 1));
        var result = await _repository.SaveAsync(visit, Ct);
        Assert.That(result.IsSuccess, Is.True, "przygotowanie: zapis wizyty");
    }

    private async Task<List<OutboxMessage>> ReadOutboxAsync()
    {
        var connection = await _db.GetConnectionAsync();
        return await connection.QueryAsync<OutboxMessage>("SELECT * FROM outbox ORDER BY CreatedAt, rowid");
    }

    private async Task<List<OutboxMessage>> ReadOutboxAsync(VisitId id) =>
        (await ReadOutboxAsync()).Where(m => m.VisitId == Key(id)).ToList();

    private async Task<int> ServerVersionAsync(VisitId id)
    {
        var connection = await _db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT ServerVersion FROM visits WHERE Id = ?", Key(id));
    }

    private async Task<int> VisitRowCountAsync(VisitId id)
    {
        var connection = await _db.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM visits WHERE Id = ?", Key(id));
    }

    private async Task ExecuteAsync(string sql)
    {
        var connection = await _db.GetConnectionAsync();
        await connection.ExecuteAsync(sql);
    }

    // Brama odpowiada według funkcji; każde wywołanie trafia do _calls
    private void SenderResponds(Func<OutboxMessage, int, Result<int>> respond)
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<OutboxMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((OutboxMessage message, int expectedVersion, CancellationToken _) =>
            {
                _calls.Add((message.Id, message.Type, message.VisitId, expectedVersion));
                return Task.FromResult(respond(message, expectedVersion));
            });
    }

    // ---------- SaveAsync: zmiana i wiadomości w jednej transakcji ----------

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task SaveAsync_SeveralEvents_SavesMessageForEachInOrder()
    {
        var id = NewId();
        var visit = await AddInProgressAsync(id);
        visit.RecordActivity("DIAG", "Diagnostyka", At(1));
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(2));
        visit.SignByCustomer("Anna Nowak", At(3));
        visit.Complete(VisitOutcome.Create(VisitOutcomeType.Fixed, null).Value, At(4));

        var result = await _repository.SaveAsync(visit, Ct);

        Assert.That(result.IsSuccess, Is.True);
        var messages = await ReadOutboxAsync();
        Assert.That(messages.Select(m => m.Type),
            Is.EqualTo(new[] { "ActivityRecorded", "PartUsed", "CustomerSigned", "VisitCompleted" }));
        Assert.That(messages.Select(m => m.VisitId), Is.All.EqualTo(Key(id)));
        Assert.That(messages.Select(m => m.Id).Distinct().Count(), Is.EqualTo(4));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task SaveAsync_NewMessage_IsPendingWithNoAttempts()
    {
        var id = NewId();
        await SaveOnePartAsync(id, 1);

        var message = (await ReadOutboxAsync()).Single();

        Assert.That(message.Attempts, Is.EqualTo(0));
        Assert.That(message.SentAt, Is.Null);
        Assert.That(message.Failed, Is.False);
        Assert.That(message.LastError, Is.Null);
        Assert.That(DateTimeOffset.Parse(message.CreatedAt), Is.EqualTo(At(1)));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task SaveAsync_SkippedEvent_DoesNotSaveMessageForIt()
    {
        // zmiana zakresu po podpisie: PartUsed + unieważnienie podpisu (niewysyłane)
        var id = NewId();
        await _repository.AddAsync(TestVisits.Signed(id), Ct);
        var visit = TestVisits.Signed(id);
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(1));

        await _repository.SaveAsync(visit, Ct);

        var messages = await ReadOutboxAsync();
        Assert.That(messages.Select(m => m.Type), Is.EqualTo(new[] { "PartUsed" }));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task SaveAsync_Success_CallsRequestSyncOnce()
    {
        var id = NewId();
        var visit = await AddInProgressAsync(id);
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(1));
        visit.RecordActivity("DIAG", "Diagnostyka", At(2));

        await _repository.SaveAsync(visit, Ct);

        _sync.Verify(s => s.RequestSync(), Times.Once);
    }

    [Test]
    public async Task SaveAsync_Success_ClearsVisitDomainEvents()
    {
        var id = NewId();
        var visit = await AddInProgressAsync(id);
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(1));

        await _repository.SaveAsync(visit, Ct);

        Assert.That(visit.DomainEvents, Is.Empty);
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task SaveAsync_VersionConflict_ReturnsVersionConflictWithNoNewMessages()
    {
        var id = NewId();
        var first = await AddInProgressAsync(id);
        var stale = TestVisits.InProgress(id);   // kopia o tej samej (za chwilę nieaktualnej) wersji
        first.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(1));
        await _repository.SaveAsync(first, Ct);
        stale.RecordActivity("DIAG", "Diagnostyka", At(2));

        var result = await _repository.SaveAsync(stale, Ct);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(VisitErrors.VersionConflict));
        var messages = await ReadOutboxAsync();
        Assert.That(messages.Select(m => m.Type), Is.EqualTo(new[] { "PartUsed" }));
    }

    [Test]
    public async Task SaveAsync_VersionConflict_DoesNotCallRequestSync()
    {
        var id = NewId();
        var first = await AddInProgressAsync(id);
        var stale = TestVisits.InProgress(id);
        first.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(1));
        await _repository.SaveAsync(first, Ct);
        _sync.Invocations.Clear();
        stale.RecordActivity("DIAG", "Diagnostyka", At(2));

        await _repository.SaveAsync(stale, Ct);

        _sync.Verify(s => s.RequestSync(), Times.Never);
    }

    [Test]
    public async Task GetLocallyChangedStatusesAsync_NoSavedChanges_ReturnsEmpty()
    {
        await _repository.AddAsync(TestVisits.InProgress(NewId()), Ct);

        var changed = await _repository.GetLocallyChangedStatusesAsync(Ct);

        Assert.That(changed, Is.Empty);
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task GetLocallyChangedStatusesAsync_PendingMessage_ReturnsVisitWithStatus()
    {
        var id = NewId();
        await _repository.AddAsync(TestVisits.Planned(id), Ct);
        var visit = TestVisits.Planned(id);
        visit.Start(At(1));
        await _repository.SaveAsync(visit, Ct);

        var changed = await _repository.GetLocallyChangedStatusesAsync(Ct);

        Assert.That(changed.Keys, Is.EquivalentTo(new[] { id.Value }));
        Assert.That(changed[id.Value], Is.EqualTo(VisitStatus.InProgress));
    }

    [Test]
    public async Task GetLocallyChangedStatusesAsync_AllSent_DoesNotReturnVisit()
    {
        var id = NewId();
        await SaveOnePartAsync(id, 1);
        await ExecuteAsync("UPDATE outbox SET SentAt = '2026-03-10T10:00:00.0000000+00:00'");

        var changed = await _repository.GetLocallyChangedStatusesAsync(Ct);

        Assert.That(changed.Keys, Does.Not.Contain(id.Value));
    }

    [Test]
    public async Task GetLocallyChangedStatusesAsync_MessagesFailed_DoesNotReturnVisit()
    {
        var id = NewId();
        await SaveOnePartAsync(id, 1);
        await ExecuteAsync("UPDATE outbox SET Failed = 1");

        var changed = await _repository.GetLocallyChangedStatusesAsync(Ct);

        Assert.That(changed.Keys, Does.Not.Contain(id.Value));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task SeedAsync_PendingMessages_DoesNotOverwriteVisit()
    {
        var id = NewId();
        await _repository.AddAsync(TestVisits.Planned(id), Ct);
        var visit = TestVisits.Planned(id);
        visit.Start(At(1));
        await _repository.SaveAsync(visit, Ct);

        await _repository.SeedAsync(Remote(TestVisits.Planned(id)), Ct);

        var details = await _repository.GetDetailsAsync(id, Ct);
        Assert.That(details!.Status, Is.EqualTo(VisitStatus.InProgress));
    }

    [Test]
    public async Task SeedAsync_AllSent_OverwritesVisit()
    {
        var id = NewId();
        await _repository.AddAsync(TestVisits.Planned(id), Ct);
        var visit = TestVisits.Planned(id);
        visit.Start(At(1));
        await _repository.SaveAsync(visit, Ct);
        await ExecuteAsync("UPDATE outbox SET SentAt = '2026-03-10T10:00:00.0000000+00:00'");

        await _repository.SeedAsync(Remote(TestVisits.Planned(id)), Ct);

        var details = await _repository.GetDetailsAsync(id, Ct);
        Assert.That(details!.Status, Is.EqualTo(VisitStatus.Planned));
    }

    [Test]
    public async Task SeedAsync_MessagesFailed_OverwritesVisit()
    {
        var id = NewId();
        await _repository.AddAsync(TestVisits.Planned(id), Ct);
        var visit = TestVisits.Planned(id);
        visit.Start(At(1));
        await _repository.SaveAsync(visit, Ct);
        await ExecuteAsync("UPDATE outbox SET Failed = 1");

        await _repository.SeedAsync(Remote(TestVisits.Planned(id)), Ct);

        var details = await _repository.GetDetailsAsync(id, Ct);
        Assert.That(details!.Status, Is.EqualTo(VisitStatus.Planned));
    }

    // ---------- OutboxProcessor.ProcessAsync: wysyłka kolejki ----------

    [Test]
    public async Task ProcessAsync_EmptyQueue_ReturnsZerosAndDoesNotSend()
    {
        SenderResponds((_, _) => SendResults.Ok(1));

        var result = await _processor.ProcessAsync(Ct);

        Assert.That(result.Sent, Is.EqualTo(0));
        Assert.That(result.Pending, Is.EqualTo(0));
        Assert.That(result.Conflicts, Is.Empty);
        Assert.That(_calls, Is.Empty);
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_Success_SetsSentAt()
    {
        var id = NewId();
        await SaveOnePartAsync(id, 1);
        SenderResponds((_, _) => SendResults.Ok(42));

        await _processor.ProcessAsync(Ct);

        var message = (await ReadOutboxAsync()).Single();
        Assert.That(message.SentAt, Is.Not.Null);
        Assert.That(message.Failed, Is.False);
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_FirstMessage_SendsWithVisitServerVersion()
    {
        var id = NewId();
        await SaveOnePartAsync(id, 1);
        var serverVersion = await ServerVersionAsync(id);
        SenderResponds((_, _) => SendResults.Ok(42));

        await _processor.ProcessAsync(Ct);

        Assert.That(_calls.Single().ExpectedVersion, Is.EqualTo(serverVersion));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_Success_SetsServerVersionFromResponse()
    {
        var id = NewId();
        await SaveOnePartAsync(id, 1);
        SenderResponds((_, _) => SendResults.Ok(42));

        await _processor.ProcessAsync(Ct);

        Assert.That(await ServerVersionAsync(id), Is.EqualTo(42));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_TwoVisitMessages_SecondGoesWithNewVersion()
    {
        var id = NewId();
        await SavePartAndActivityAsync(id, 1);
        var serverVersion = await ServerVersionAsync(id);
        var nextVersion = 100;
        SenderResponds((_, _) => SendResults.Ok(nextVersion++));

        await _processor.ProcessAsync(Ct);

        Assert.That(_calls.Select(c => c.Type), Is.EqualTo(new[] { "PartUsed", "ActivityRecorded" }));
        Assert.That(_calls[0].ExpectedVersion, Is.EqualTo(serverVersion));
        Assert.That(_calls[1].ExpectedVersion, Is.EqualTo(100));
        Assert.That(await ServerVersionAsync(id), Is.EqualTo(101));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_SeveralVisits_SendsInSaveOrderNotClockOrder()
    {
        // wizyta A zapisana pierwsza, ale ze zdarzeniem późniejszym niż wizyta B (zegar telefonu mógł się cofnąć)
        var visitA = NewId();
        var visitB = NewId();
        await SaveOnePartAsync(visitA, 10);
        await SaveOnePartAsync(visitB, 5);
        SenderResponds((_, _) => SendResults.Ok(50));

        await _processor.ProcessAsync(Ct);

        Assert.That(_calls.Select(c => c.VisitId), Is.EqualTo(new[] { Key(visitA), Key(visitB) }));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_Network_StopsRunWithoutChangingAttempts()
    {
        await SaveOnePartAsync(NewId(), 1);
        await SaveOnePartAsync(NewId(), 2);
        SenderResponds((_, _) => SendResults.Fail(SendResults.Network));

        var result = await _processor.ProcessAsync(Ct);

        Assert.That(_calls, Has.Count.EqualTo(1));
        var messages = await ReadOutboxAsync();
        Assert.That(messages.Select(m => m.Attempts), Is.All.EqualTo(0));
        Assert.That(messages.Select(m => m.SentAt), Is.All.Null);
        Assert.That(messages.Select(m => m.Failed), Is.All.False);
        Assert.That(result.Sent, Is.EqualTo(0));
        Assert.That(result.Pending, Is.EqualTo(2));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_Unauthorized_StopsRunWithoutChangingAttempts()
    {
        await SaveOnePartAsync(NewId(), 1);
        await SaveOnePartAsync(NewId(), 2);
        SenderResponds((_, _) => SendResults.Fail(SendResults.Unauthorized));

        var result = await _processor.ProcessAsync(Ct);

        Assert.That(_calls, Has.Count.EqualTo(1));
        var messages = await ReadOutboxAsync();
        Assert.That(messages.Select(m => m.Attempts), Is.All.EqualTo(0));
        Assert.That(messages.Select(m => m.Failed), Is.All.False);
        Assert.That(result.Pending, Is.EqualTo(2));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_OtherError_IncrementsAttemptsAndSavesLastError()
    {
        await SaveOnePartAsync(NewId(), 1);
        SenderResponds((_, _) => SendResults.Fail(SendResults.Unexpected));

        await _processor.ProcessAsync(Ct);

        var message = (await ReadOutboxAsync()).Single();
        Assert.That(message.Attempts, Is.EqualTo(1));
        Assert.That(message.LastError, Is.EqualTo("Visits.Unexpected"));
        Assert.That(message.SentAt, Is.Null);
        Assert.That(message.Failed, Is.False);
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_OtherError_SkipsFurtherMessagesForThatVisitAndSendsOtherVisits()
    {
        var failing = NewId();
        var other = NewId();
        await SavePartAndActivityAsync(failing, 1);   // minuty 1 i 2
        await SaveOnePartAsync(other, 3);
        SenderResponds((message, _) => message.VisitId == Key(failing)
            ? SendResults.Fail(SendResults.Unexpected)
            : SendResults.Ok(60));

        var result = await _processor.ProcessAsync(Ct);

        Assert.That(_calls.Select(c => (c.VisitId, c.Type)),
            Is.EqualTo(new[] { (Key(failing), "PartUsed"), (Key(other), "PartUsed") }));
        var failingMessages = await ReadOutboxAsync(failing);
        Assert.That(failingMessages[0].Attempts, Is.EqualTo(1));
        Assert.That(failingMessages[1].Attempts, Is.EqualTo(0));
        Assert.That((await ReadOutboxAsync(other)).Single().SentAt, Is.Not.Null);
        Assert.That(result.Sent, Is.EqualTo(1));
        Assert.That(result.Pending, Is.EqualTo(2));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_FewerThanMaxAttempts_DoesNotMarkFailed()
    {
        await SaveOnePartAsync(NewId(), 1);
        SenderResponds((_, _) => SendResults.Fail(SendResults.Unexpected));

        for (var i = 0; i < OutboxProcessor.MaxAttempts - 1; i++)
            await _processor.ProcessAsync(Ct);

        var message = (await ReadOutboxAsync()).Single();
        Assert.That(message.Attempts, Is.EqualTo(OutboxProcessor.MaxAttempts - 1));
        Assert.That(message.Failed, Is.False);
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_MaxAttemptsReached_MarksFailed()
    {
        await SaveOnePartAsync(NewId(), 1);
        SenderResponds((_, _) => SendResults.Fail(SendResults.Unexpected));

        for (var i = 0; i < OutboxProcessor.MaxAttempts; i++)
            await _processor.ProcessAsync(Ct);

        var message = (await ReadOutboxAsync()).Single();
        Assert.That(OutboxProcessor.MaxAttempts, Is.EqualTo(5));
        Assert.That(message.Attempts, Is.EqualTo(5));
        Assert.That(message.Failed, Is.True);
        Assert.That(message.SentAt, Is.Null);
    }

    [Test]
    public async Task ProcessAsync_FailedMessage_IsNoLongerSent()
    {
        await SaveOnePartAsync(NewId(), 1);
        SenderResponds((_, _) => SendResults.Fail(SendResults.Unexpected));
        for (var i = 0; i < OutboxProcessor.MaxAttempts; i++)
            await _processor.ProcessAsync(Ct);
        _calls.Clear();

        var result = await _processor.ProcessAsync(Ct);

        Assert.That(_calls, Is.Empty);
        Assert.That(result.Pending, Is.EqualTo(0));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_RetryAfterError_SendsSameIdAsIdempotencyKey()
    {
        await SaveOnePartAsync(NewId(), 1);
        var storedId = (await ReadOutboxAsync()).Single().Id;
        var attempt = 0;
        SenderResponds((_, _) => attempt++ == 0 ? SendResults.Fail(SendResults.Unexpected) : SendResults.Ok(7));

        await _processor.ProcessAsync(Ct);
        await _processor.ProcessAsync(Ct);

        Assert.That(_calls.Select(c => c.Id), Is.EqualTo(new[] { storedId, storedId }));
        Assert.That((await ReadOutboxAsync()).Single().SentAt, Is.Not.Null);
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_RetryAfterNetworkFailure_SendsSameId()
    {
        await SaveOnePartAsync(NewId(), 1);
        var storedId = (await ReadOutboxAsync()).Single().Id;
        var attempt = 0;
        SenderResponds((_, _) => attempt++ == 0 ? SendResults.Fail(SendResults.Network) : SendResults.Ok(7));

        await _processor.ProcessAsync(Ct);
        await _processor.ProcessAsync(Ct);

        Assert.That(_calls.Select(c => c.Id), Is.EqualTo(new[] { storedId, storedId }));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_VersionConflict_MarksAllPendingVisitMessagesFailed()
    {
        var id = NewId();
        await SavePartAndActivityAsync(id, 1);
        SenderResponds((_, _) => SendResults.Fail(VisitErrors.VersionConflict));

        await _processor.ProcessAsync(Ct);

        var messages = await ReadOutboxAsync(id);
        Assert.That(messages, Has.Count.EqualTo(2));
        Assert.That(messages.Select(m => m.Failed), Is.All.True);
        Assert.That(messages.Select(m => m.LastError), Is.All.EqualTo("Visit.VersionConflict"));
        Assert.That(messages.Select(m => m.SentAt), Is.All.Null);
        Assert.That(_calls, Has.Count.EqualTo(1));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_VersionConflict_RemovesVisitRowFromReplica()
    {
        var id = NewId();
        await SaveOnePartAsync(id, 1);
        SenderResponds((_, _) => SendResults.Fail(VisitErrors.VersionConflict));

        await _processor.ProcessAsync(Ct);

        Assert.That(await VisitRowCountAsync(id), Is.EqualTo(0));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_VersionConflict_ReturnsVisitInConflictsAndSendsOtherVisits()
    {
        var conflicted = NewId();
        var other = NewId();
        await SavePartAndActivityAsync(conflicted, 1);
        await SaveOnePartAsync(other, 5);
        SenderResponds((message, _) => message.VisitId == Key(conflicted)
            ? SendResults.Fail(VisitErrors.VersionConflict)
            : SendResults.Ok(9));

        var result = await _processor.ProcessAsync(Ct);

        Assert.That(result.Conflicts, Is.EqualTo(new[] { conflicted }));
        Assert.That(result.Sent, Is.EqualTo(1));
        Assert.That(result.Pending, Is.EqualTo(0));
        Assert.That((await ReadOutboxAsync(other)).Single().SentAt, Is.Not.Null);
        Assert.That(await VisitRowCountAsync(other), Is.EqualTo(1));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_AllSent_ReturnsSentAndPendingZero()
    {
        await SavePartAndActivityAsync(NewId(), 1);
        await SaveOnePartAsync(NewId(), 5);
        SenderResponds((_, _) => SendResults.Ok(3));

        var result = await _processor.ProcessAsync(Ct);

        Assert.That(result.Sent, Is.EqualTo(3));
        Assert.That(result.Pending, Is.EqualTo(0));
        Assert.That(result.Conflicts, Is.Empty);
    }

    [Test]
    public async Task ProcessAsync_MessagesAlreadySent_DoesNotResend()
    {
        await SaveOnePartAsync(NewId(), 1);
        SenderResponds((_, _) => SendResults.Ok(3));
        await _processor.ProcessAsync(Ct);
        _calls.Clear();

        var result = await _processor.ProcessAsync(Ct);

        Assert.That(_calls, Is.Empty);
        Assert.That(result.Sent, Is.EqualTo(0));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task CountPendingAsync_SavedChanges_ReturnsPendingCount()
    {
        await SavePartAndActivityAsync(NewId(), 1);
        await SaveOnePartAsync(NewId(), 5);

        var count = await _processor.CountPendingAsync(Ct);

        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task CountPendingAsync_SentAndFailed_AreNotCounted()
    {
        var sent = NewId();
        var conflicted = NewId();
        var pending = NewId();
        await SaveOnePartAsync(sent, 1);
        await SavePartAndActivityAsync(conflicted, 2);
        await SaveOnePartAsync(pending, 4);
        SenderResponds((message, _) =>
            message.VisitId == Key(sent) ? SendResults.Ok(5)
            : message.VisitId == Key(conflicted) ? SendResults.Fail(VisitErrors.VersionConflict)
            : SendResults.Fail(SendResults.Network));
        await _processor.ProcessAsync(Ct);

        var count = await _processor.CountPendingAsync(Ct);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task SaveAsync_OutboxInsertFails_RollsBackVisitChange()
    {
        var id = NewId();
        var visit = await AddInProgressAsync(id);
        // zapis do outbox się nie uda – zmiana wizyty nie może zostać sama (całość albo nic)
        await ExecuteAsync("CREATE TRIGGER fail_outbox BEFORE INSERT ON outbox BEGIN SELECT RAISE(ABORT, 'test'); END");
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, At(1));

        Assert.ThrowsAsync<SQLite.SQLiteException>(() => _repository.SaveAsync(visit, Ct));

        var loaded = await _repository.GetAsync(id, Ct);
        Assert.That(loaded!.Scope.Parts, Is.Empty);
        Assert.That(await ReadOutboxAsync(), Is.Empty);
        _sync.Verify(s => s.RequestSync(), Times.Never);
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_MaxAttemptsReached_RejectsWholeVisit()
    {
        var id = NewId();
        await SavePartAndActivityAsync(id, 1);
        SenderResponds((_, _) => SendResults.Fail(SendResults.Unexpected));

        SyncResult last = null!;
        for (var i = 0; i < OutboxProcessor.MaxAttempts; i++)
            last = await _processor.ProcessAsync(Ct);

        var messages = await ReadOutboxAsync(id);
        Assert.Multiple(async () =>
        {
            Assert.That(messages.Select(m => m.Failed), Is.All.True);
            Assert.That(await VisitRowCountAsync(id), Is.EqualTo(0));
            Assert.That(last.Rejected, Is.EqualTo(new[] { id }));
            Assert.That(last.Conflicts, Is.Empty);
            Assert.That(last.Pending, Is.EqualTo(0));
        });
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task ProcessAsync_SenderThrows_CountsAttemptAndSendsOtherVisits()
    {
        var broken = NewId();
        var other = NewId();
        await SaveOnePartAsync(broken, 1);
        await SaveOnePartAsync(other, 2);
        SenderResponds((message, _) => message.VisitId == Key(broken)
            ? throw new InvalidOperationException("nieczytelna odpowiedź")
            : SendResults.Ok(3));

        var result = await _processor.ProcessAsync(Ct);

        var failed = (await ReadOutboxAsync(broken)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Sent, Is.EqualTo(1));
            Assert.That(failed.Attempts, Is.EqualTo(1));
            Assert.That(failed.LastError, Is.EqualTo("Outbox.Unexpected"));
        });
    }

    [Test]
    [Ignore("Ćwiczenie 11: usuń Ignore po uzupełnieniu TODO")]
    public async Task SeedAsync_OlderServerVersion_DoesNotLowerServerVersion()
    {
        var id = NewId();
        await SaveOnePartAsync(id, 1);
        SenderResponds((_, _) => SendResults.Ok(7));
        await _processor.ProcessAsync(Ct);

        // odpowiedź na odczyt wysłany przed zmianą wraca po niej – ze starszą wersją
        await _repository.SeedAsync(Remote(TestVisits.InProgress(id)), Ct);

        Assert.That(await ServerVersionAsync(id), Is.EqualTo(7));
    }
}
