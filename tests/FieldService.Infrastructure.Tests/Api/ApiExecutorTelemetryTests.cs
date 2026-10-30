using System.Diagnostics;
using FieldService.Application.Diagnostics;
using FieldService.Infrastructure.Api;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Api;

// Wykonanie wokół: każde wywołanie API przez ApiExecutor = jeden span o nazwie operacji
[TestFixture]
public sealed class ApiExecutorTelemetryTests
{
    private const string Operation = "parts.reserve";

    private ActivityListener _listener = null!;
    private List<Activity> _spans = null!;

    [SetUp]
    public void SetUp()
    {
        _spans = [];
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AppTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                // tylko spany tej operacji – inne testy mogą tworzyć własne
                if (activity.OperationName == Operation)
                {
                    lock (_spans) { _spans.Add(activity); }
                }
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [TearDown]
    public void TearDown()
    {
        _listener.Dispose();
    }

    private static ApiExecutor CreateExecutor() => new(NullLogger<ApiExecutor>.Instance);

    [Test]
    public async Task ExecuteAsync_Success_CreatesSpanNamedAfterOperationWithoutError()
    {
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync(Operation, _ => Task.FromResult(42), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_spans, Has.Count.EqualTo(1));
        var span = _spans[0];
        Assert.That(span.Source.Name, Is.EqualTo(AppTelemetry.Name));
        Assert.That(span.Status, Is.AnyOf(ActivityStatusCode.Unset, ActivityStatusCode.Ok));
        Assert.That(span.GetTagItem("error.type"), Is.Null);
    }

    [Test]
    public async Task ExecuteAsync_NetworkError_SpanHasErrorStatusAndNetworkErrorType()
    {
        var executor = CreateExecutor();

        // ZAŁOŻENIE (jak w 13): HttpRequestException bez odpowiedzi serwera → Result.Failure z ErrorType.Network
        var result = await executor.ExecuteAsync<int>(
            Operation,
            _ => throw new HttpRequestException("Brak połączenia"),
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(_spans, Has.Count.EqualTo(1));
        var span = _spans[0];
        Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error));
        Assert.That(span.GetTagItem("error.type"), Is.EqualTo("network"));
    }

    [Test]
    public async Task ExecuteAsync_NetworkError_ErrorTypeTagMatchesResultErrorType()
    {
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync<int>(
            Operation,
            _ => throw new HttpRequestException("Brak połączenia"),
            CancellationToken.None);

        // tag = nazwa ErrorType małymi literami (kontrakt 15)
        Assert.That(_spans[0].GetTagItem("error.type"), Is.EqualTo(result.Error!.Type.ToString().ToLowerInvariant()));
    }

    [Test]
    public void ExecuteAsync_Cancelled_ThrowsAndClosesSpan()
    {
        var executor = CreateExecutor();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // anulowanie – wyjątek jak w 13 (OperationCanceledException lub pochodny)
        Assert.That(
            async () => await executor.ExecuteAsync<int>(
                Operation,
                ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(1);
                },
                cts.Token),
            Throws.InstanceOf<OperationCanceledException>());

        // span zamknięty (ActivityStopped wywołane)
        Assert.That(_spans, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_TwoCalls_CreatesSeparateSpans()
    {
        var executor = CreateExecutor();

        await executor.ExecuteAsync(Operation, _ => Task.FromResult(1), CancellationToken.None);
        await executor.ExecuteAsync(Operation, _ => Task.FromResult(2), CancellationToken.None);

        Assert.That(_spans, Has.Count.EqualTo(2));
        Assert.That(_spans[0].SpanId, Is.Not.EqualTo(_spans[1].SpanId));
    }

    [Test]
    public async Task ExecuteAsync_InsideParentSpan_SpanIsChildOfCurrentActivity()
    {
        var executor = CreateExecutor();
        // ZAŁOŻENIE: span ApiExecutor tworzony standardowo (StartActivity) – rodzicem jest Activity.Current
        using var parent = AppTelemetry.Source.StartActivity("test.parent");
        Assert.That(parent, Is.Not.Null);

        await executor.ExecuteAsync(Operation, _ => Task.FromResult(1), CancellationToken.None);

        Assert.That(_spans, Has.Count.EqualTo(1));
        Assert.That(_spans[0].TraceId, Is.EqualTo(parent!.TraceId));
        Assert.That(_spans[0].ParentSpanId, Is.EqualTo(parent.SpanId));
    }
}
