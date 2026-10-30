using System.Diagnostics;
using System.Diagnostics.Metrics;
using FieldService.Application.Diagnostics;
using FieldService.Application.Visits.Commands;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;
using Activity = System.Diagnostics.Activity;
using NUnit.Framework;

namespace FieldService.Application.Tests.Visits;

// Telemetria zakończenia wizyty: span "CompleteVisit" z tagiem visit.id i licznik fieldservice.visits.completed
[TestFixture]
public sealed class CompleteVisitHandlerTelemetryTests
{
    private const string SpanName = "CompleteVisit";
    private const string CounterName = "fieldservice.visits.completed";

    // ZAŁOŻENIE: stała chwila zgodna z wizytą z TestVisits.Signed() (wizyta w toku – można ją zakończyć)
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private ActivityListener _activityListener = null!;
    private MeterListener _meterListener = null!;
    private List<Activity> _spans = null!;
    private List<(long Value, Dictionary<string, object?> Tags)> _measurements = null!;

    [SetUp]
    public void SetUp()
    {
        _spans = [];
        _measurements = [];

        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AppTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                // filtrujemy po nazwie – inne testy mogą tworzyć własne spany
                if (activity.OperationName == SpanName)
                {
                    lock (_spans) { _spans.Add(activity); }
                }
            }
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AppTelemetry.Name && instrument.Name == CounterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var copy = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                copy[tag.Key] = tag.Value;
            }
            lock (_measurements) { _measurements.Add((value, copy)); }
        });
        _meterListener.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }

    private static (CompleteVisitHandler Handler, Visit Visit) CreateHandlerWithSignedVisit()
    {
        var visit = TestVisits.Signed();
        var repository = new InMemoryVisitRepository();
        repository.AddAsync(visit, CancellationToken.None).GetAwaiter().GetResult();
        var handler = new CompleteVisitHandler(repository, new FixedClock(Now));
        return (handler, visit);
    }

    [Test]
    public async Task HandleAsync_VisitCompleted_CreatesCompleteVisitSpanWithVisitId()
    {
        var (handler, visit) = CreateHandlerWithSignedVisit();

        // ZAŁOŻENIE: metoda obsługi to HandleAsync(command, ct), a VisitId ma właściwość Value (Guid)
        var result = await handler.HandleAsync(new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_spans, Has.Count.EqualTo(1));
        var span = _spans[0];
        Assert.That(span.Source.Name, Is.EqualTo(AppTelemetry.Name));
        Assert.That(span.GetTagItem("visit.id"), Is.EqualTo(visit.Id.Value.ToString()));
        Assert.That(span.Status, Is.Not.EqualTo(ActivityStatusCode.Error));
        Assert.That(span.GetTagItem("error.type"), Is.Null);
    }

    [Test]
    public async Task HandleAsync_VisitCompleted_IncrementsCounterByOneWithOutcome()
    {
        var (handler, visit) = CreateHandlerWithSignedVisit();

        var result = await handler.HandleAsync(new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_measurements, Has.Count.EqualTo(1));
        Assert.That(_measurements[0].Value, Is.EqualTo(1));
        Assert.That(_measurements[0].Tags["outcome"]?.ToString(), Is.EqualTo("Fixed"));
    }

    [Test]
    public async Task HandleAsync_VisitCompleted_SpanHasNoPersonalDataTags()
    {
        var (handler, visit) = CreateHandlerWithSignedVisit();

        await handler.HandleAsync(new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.That(_spans, Has.Count.EqualTo(1));
        // bez nazwiska klienta i podpisu w tagach – sprawdzamy nazwy tagów
        var tagKeys = _spans[0].TagObjects.Select(t => t.Key.ToLowerInvariant()).ToList();
        Assert.That(tagKeys.Any(k => k.Contains("customer")), Is.False, "customer");
        Assert.That(tagKeys.Any(k => k.Contains("signature")), Is.False, "signature");
        Assert.That(tagKeys.Any(k => k.Contains("signer")), Is.False, "signer");
    }

    [Test]
    public async Task HandleAsync_UnknownVisit_SpanHasErrorStatusAndErrorType()
    {
        var (handler, _) = CreateHandlerWithSignedVisit();
        // ZAŁOŻENIE: VisitId to rekord z konstruktorem (Guid); wizyty o tym id nie ma w repozytorium
        var unknownId = new VisitId(Guid.Parse("7c1e5f2a-0000-4000-8000-000000000015"));

        var result = await handler.HandleAsync(new CompleteVisit(unknownId, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(_spans, Has.Count.EqualTo(1));
        var span = _spans[0];
        Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error));
        // error.type = nazwa ErrorType małymi literami (kontrakt 15)
        Assert.That(span.GetTagItem("error.type"), Is.EqualTo(result.Error!.Type.ToString().ToLowerInvariant()));
        Assert.That(span.GetTagItem("visit.id"), Is.EqualTo(unknownId.Value.ToString()));
    }

    [Test]
    public async Task HandleAsync_UnknownVisit_DoesNotIncrementCounter()
    {
        var (handler, _) = CreateHandlerWithSignedVisit();
        var unknownId = new VisitId(Guid.Parse("7c1e5f2a-0000-4000-8000-000000000015"));

        var result = await handler.HandleAsync(new CompleteVisit(unknownId, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(_measurements, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_VisitCompletedTwice_SecondAttemptFailsAndCounterStaysAtOne()
    {
        var (handler, visit) = CreateHandlerWithSignedVisit();

        await handler.HandleAsync(new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);
        // ZAŁOŻENIE: ponowne zakończenie zakończonej wizyty to błąd reguły domeny (Result.Failure)
        var second = await handler.HandleAsync(new CompleteVisit(visit.Id, VisitOutcomeType.Fixed, null), CancellationToken.None);

        Assert.That(second.IsSuccess, Is.False);
        Assert.That(_measurements, Has.Count.EqualTo(1));
        Assert.That(_spans, Has.Count.EqualTo(2));
        Assert.That(_spans[1].Status, Is.EqualTo(ActivityStatusCode.Error));
        Assert.That(_spans[1].GetTagItem("error.type"), Is.EqualTo(second.Error!.Type.ToString().ToLowerInvariant()));
    }
}
