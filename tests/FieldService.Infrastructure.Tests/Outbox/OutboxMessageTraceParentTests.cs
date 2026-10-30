using System.Diagnostics;
using FieldService.Application.Diagnostics;
using FieldService.Domain.Visits;
using FieldService.Domain.Visits.Events;
using FieldService.Infrastructure.Outbox;
using Activity = System.Diagnostics.Activity;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Outbox;

// Outbox: zapis i wysyłka to dwa ślady – przy zapisie zapamiętujemy kontekst (traceparent) bieżącego spanu
[TestFixture]
public sealed class OutboxMessageTraceParentTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private ActivityListener _listener = null!;
    private Activity? _previousCurrent;

    [SetUp]
    public void SetUp()
    {
        // bez śladu „odziedziczonego” z innego testu
        _previousCurrent = Activity.Current;
        Activity.Current = null;

        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AppTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [TearDown]
    public void TearDown()
    {
        _listener.Dispose();
        Activity.Current = _previousCurrent;
    }

    // OutboxMessage.From(IDomainEvent) – zdarzenie rozpoczęcia wizyty
    private static OutboxMessage CreateMessage() =>
        OutboxMessage.From(new VisitStarted(new VisitId(Guid.Parse("7c1e5f2a-0000-4000-8000-000000000015")), Now))!;

    [Test]
    public void From_InsideActivity_TraceParentIsCurrentActivityId()
    {
        using var activity = AppTelemetry.Source.StartActivity("CompleteVisit");
        Assert.That(activity, Is.Not.Null);

        var message = CreateMessage();

        Assert.That(message.TraceParent, Is.EqualTo(activity!.Id));
    }

    [Test]
    public void From_InsideActivity_TraceParentIsW3CFormatWithTraceId()
    {
        using var activity = AppTelemetry.Source.StartActivity("CompleteVisit");
        Assert.That(activity, Is.Not.Null);

        var message = CreateMessage();

        // W3C traceparent: 00-{trace id}-{span id}-{flagi}
        Assert.That(message.TraceParent, Does.StartWith("00-"));
        Assert.That(message.TraceParent, Does.Contain(activity!.TraceId.ToHexString()));
        Assert.That(message.TraceParent, Does.Contain(activity.SpanId.ToHexString()));
    }

    [Test]
    public void From_WithoutActivity_TraceParentIsNull()
    {
        Assert.That(Activity.Current, Is.Null);

        var message = CreateMessage();

        Assert.That(message.TraceParent, Is.Null);
    }

    [Test]
    public void From_AfterActivityStopped_TraceParentIsNull()
    {
        using (AppTelemetry.Source.StartActivity("CompleteVisit"))
        {
        }

        var message = CreateMessage();

        Assert.That(message.TraceParent, Is.Null);
    }
}
