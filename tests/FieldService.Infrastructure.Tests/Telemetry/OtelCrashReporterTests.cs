using System.Diagnostics;
using FieldService.Application.Abstractions;
using FieldService.Application.Diagnostics;
using FieldService.Infrastructure.Telemetry;
using FieldService.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Telemetry;

// Adapter portu ICrashReporter: wyjątek obsłużony = zdarzenie "exception" na spanie + log Error;
// awarie zapisane w PendingCrashStore wysyłane przy starcie jako logi Critical
[TestFixture]
public sealed class OtelCrashReporterTests
{
    private const string SpanName = "test.crash-reporter";

    private TemporaryAppPaths _paths = null!;
    private RecordingLogger<OtelCrashReporter> _logger = null!;
    private ActivityListener _listener = null!;
    private List<Activity> _spans = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _logger = new RecordingLogger<OtelCrashReporter>();
        _spans = [];
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AppTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == SpanName)
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
        _paths.Dispose();
    }

    private PendingCrashStore CreateStore(string appVersion = "1.4.0") =>
        new(_paths, new TelemetryOptions { AppVersion = appVersion });

    private OtelCrashReporter CreateReporter(PendingCrashStore? store = null) =>
        new(store ?? CreateStore(), _logger);

    private static Exception Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    [Test]
    public void Reporter_Always_ImplementsCrashReporterPort()
    {
        Assert.That(CreateReporter(), Is.InstanceOf<ICrashReporter>());
    }

    [Test]
    public void RecordHandled_InsideSpan_AddsExceptionEventToCurrentSpan()
    {
        ICrashReporter reporter = CreateReporter();
        var exception = Thrown("Błąd synchronizacji");

        using (var span = AppTelemetry.Source.StartActivity(SpanName))
        {
            Assert.That(span, Is.Not.Null);
            reporter.RecordHandled(exception);
        }

        Assert.That(_spans, Has.Count.EqualTo(1));
        var events = _spans[0].Events.Where(e => e.Name == "exception").ToList();
        Assert.That(events, Has.Count.EqualTo(1));
        // atrybuty zdarzenia wg konwencji OTel (Activity.AddException)
        var tags = events[0].Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.That(tags["exception.type"], Is.EqualTo("System.InvalidOperationException"));
        Assert.That(tags["exception.message"], Is.EqualTo("Błąd synchronizacji"));
    }

    [Test]
    public void RecordHandled_InsideSpan_LogsErrorWithException()
    {
        ICrashReporter reporter = CreateReporter();
        var exception = Thrown("Błąd synchronizacji");

        using (AppTelemetry.Source.StartActivity(SpanName))
        {
            reporter.RecordHandled(exception);
        }

        var errors = _logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0].Exception, Is.SameAs(exception));
    }

    [Test]
    public void RecordHandled_WithoutSpan_LogsErrorAndDoesNotThrow()
    {
        ICrashReporter reporter = CreateReporter();
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            var exception = Thrown("Błąd bez spanu");

            Assert.DoesNotThrow(() => reporter.RecordHandled(exception));

            var errors = _logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Exception, Is.SameAs(exception));
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Test]
    public void RecordHandled_WithContext_LogContainsContextValues()
    {
        ICrashReporter reporter = CreateReporter();
        var context = new Dictionary<string, string> { ["operation"] = "outbox.send" };

        reporter.RecordHandled(Thrown("Błąd"), context);

        var error = _logger.Entries.Single(e => e.Level == LogLevel.Error);
        // ZAŁOŻENIE: kontekst trafia do logu jako pary klucz–wartość (w stanie wpisu albo w zakresie BeginScope)
        Assert.That(error.Value("operation")?.ToString(), Is.EqualTo("outbox.send"));
    }

    [Test]
    public void RecordHandled_Always_DoesNotSaveToPendingStore()
    {
        var store = CreateStore();
        ICrashReporter reporter = CreateReporter(store);

        reporter.RecordHandled(Thrown("Błąd obsłużony"));

        // ZAŁOŻENIE: wyjątek obsłużony idzie od razu przez OTel – plik awarii jest tylko dla awarii nieobsłużonych
        Assert.That(store.ReadAll(), Is.Empty);
    }

    [Test]
    public async Task SendPendingAsync_SavedCrash_LogsCriticalWithExceptionAttributes()
    {
        var store = CreateStore(appVersion: "1.4.0");
        var exception = Thrown("Wymuszona awaria");
        store.Save(exception);
        var reporter = CreateReporter(store);

        await reporter.SendPendingAsync(CancellationToken.None);

        var critical = _logger.Entries.Where(e => e.Level == LogLevel.Critical).ToList();
        Assert.That(critical, Has.Count.EqualTo(1));
        var entry = critical[0];
        Assert.That(entry.Value("exception.type")?.ToString(), Is.EqualTo("System.InvalidOperationException"));
        Assert.That(entry.Value("exception.message")?.ToString(), Is.EqualTo("Wymuszona awaria"));
        Assert.That(entry.Value("exception.stacktrace")?.ToString(), Is.EqualTo(exception.ToString()));
        Assert.That(entry.Value("service.version")?.ToString(), Is.EqualTo("1.4.0"));
    }

    [Test]
    public async Task SendPendingAsync_CrashFromOlderVersion_ServiceVersionFromCrashTime()
    {
        // awaria zapisana w wersji 1.4.0, wysyłka po aktualizacji do 1.5.0
        CreateStore(appVersion: "1.4.0").Save(Thrown("Awaria w starej wersji"));
        var reporter = CreateReporter(CreateStore(appVersion: "1.5.0"));

        await reporter.SendPendingAsync(CancellationToken.None);

        var entry = _logger.Entries.Single(e => e.Level == LogLevel.Critical);
        Assert.That(entry.Value("service.version")?.ToString(), Is.EqualTo("1.4.0"));
    }

    [Test]
    public async Task SendPendingAsync_SavedCrashes_FilesDeleted()
    {
        var store = CreateStore();
        store.Save(Thrown("pierwsza"));
        Thread.Sleep(20);
        store.Save(Thrown("druga"));
        var paths = store.ReadAll().Select(c => c.Path).ToList();
        var reporter = CreateReporter(store);

        await reporter.SendPendingAsync(CancellationToken.None);

        Assert.That(store.ReadAll(), Is.Empty);
        Assert.That(paths.Any(File.Exists), Is.False);
        Assert.That(_logger.Entries.Count(e => e.Level == LogLevel.Critical), Is.EqualTo(2));
    }

    [Test]
    public async Task SendPendingAsync_CalledTwice_SendsEachCrashOnce()
    {
        var store = CreateStore();
        store.Save(Thrown("Awaria"));
        var reporter = CreateReporter(store);

        await reporter.SendPendingAsync(CancellationToken.None);
        await reporter.SendPendingAsync(CancellationToken.None);

        Assert.That(_logger.Entries.Count(e => e.Level == LogLevel.Critical), Is.EqualTo(1));
    }

    [Test]
    public async Task SendPendingAsync_NoSavedCrashes_LogsNothing()
    {
        var reporter = CreateReporter();

        await reporter.SendPendingAsync(CancellationToken.None);

        Assert.That(_logger.Entries.Where(e => e.Level >= LogLevel.Error), Is.Empty);
    }
}
