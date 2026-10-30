using System.Diagnostics;
using FieldService.Application.Abstractions;
using FieldService.Application.Diagnostics;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Outbox;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Tests.Persistence;
using FieldService.TestDoubles;
using Moq;
using NUnit.Framework;
using Activity = System.Diagnostics.Activity;

namespace FieldService.Infrastructure.Tests.Outbox;

// Zapis zmiany i jej wysyłka to dwa ślady: span „outbox.send” ma link do śladu, w którym technik zmienił wizytę
[TestFixture]
public class OutboxTraceLinkTests
{
    private TemporaryAppPaths _paths = null!;
    private SqliteDb _db = null!;
    private SqliteVisitRepository _repository = null!;
    private Mock<IOutboxSender> _sender = null!;
    private ActivityListener _listener = null!;
    private List<Activity> _spans = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _db = new SqliteDb(_paths);
        _repository = new SqliteVisitRepository(_db, Mock.Of<ISyncScheduler>());
        _sender = new Mock<IOutboxSender>();
        _sender.Setup(s => s.SendAsync(It.IsAny<OutboxMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SendResults.Ok(2));
        _spans = [];
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AppTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _spans.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [TearDown]
    public void TearDown()
    {
        _listener.Dispose();
        _paths.Dispose();
    }

    // Zmiana zapisana w spanie „CompleteVisit” → wysyłka ma link do jego kontekstu
    [Test]
    public async Task ProcessAsync_MessageSavedInsideSpan_SendSpanLinksToSaveSpan()
    {
        var id = new VisitId(Guid.NewGuid());
        await _repository.AddAsync(TestVisits.InProgress(id), CancellationToken.None);
        var visit = (await _repository.GetAsync(id, CancellationToken.None))!;
        visit.RecordActivity("DIAG", "Diagnostyka", FixedClock.DefaultNow);
        ActivityContext saveContext;
        using (var save = AppTelemetry.Source.StartActivity("CompleteVisit")!)
        {
            saveContext = save.Context;
            await _repository.SaveAsync(visit, CancellationToken.None);
        }

        await new OutboxProcessor(_db, _sender.Object).ProcessAsync(CancellationToken.None);

        var send = _spans.Single(s => s.OperationName == "outbox.send");
        Assert.That(send.Links.Select(l => l.Context.TraceId), Is.EqualTo(new[] { saveContext.TraceId }));
        Assert.That(send.GetTagItem("outbox.type"), Is.EqualTo("ActivityRecorded"));
    }

    // Wysyłka poproszona wewnątrz spanu (Task.Run przenosi kontekst) to i tak osobny ślad – bez rodzica, inny TraceId
    [Test]
    public async Task ProcessAsync_CalledInsideSpan_SendSpanStartsNewTrace()
    {
        var id = new VisitId(Guid.NewGuid());
        await _repository.AddAsync(TestVisits.InProgress(id), CancellationToken.None);
        var visit = (await _repository.GetAsync(id, CancellationToken.None))!;
        visit.RecordActivity("DIAG", "Diagnostyka", FixedClock.DefaultNow);
        ActivityTraceId saveTrace;
        using (var save = AppTelemetry.Source.StartActivity("CompleteVisit")!)
        {
            saveTrace = save.TraceId;
            await _repository.SaveAsync(visit, CancellationToken.None);
            await Task.Run(() => new OutboxProcessor(_db, _sender.Object).ProcessAsync(CancellationToken.None));
        }

        var send = _spans.Single(s => s.OperationName == "outbox.send");
        Assert.That(send.ParentSpanId, Is.EqualTo(default(ActivitySpanId)));
        Assert.That(send.TraceId, Is.Not.EqualTo(saveTrace));
    }

    // Zmiana zapisana poza śladem → wysyłka bez linku
    [Test]
    public async Task ProcessAsync_MessageSavedWithoutSpan_SendSpanHasNoLink()
    {
        var id = new VisitId(Guid.NewGuid());
        await _repository.AddAsync(TestVisits.InProgress(id), CancellationToken.None);
        var visit = (await _repository.GetAsync(id, CancellationToken.None))!;
        visit.RecordActivity("DIAG", "Diagnostyka", FixedClock.DefaultNow);
        await _repository.SaveAsync(visit, CancellationToken.None);

        await new OutboxProcessor(_db, _sender.Object).ProcessAsync(CancellationToken.None);

        Assert.That(_spans.Single(s => s.OperationName == "outbox.send").Links, Is.Empty);
    }
}
