using FieldService.Application.Abstractions;
using FieldService.Infrastructure.Telemetry;
using FieldService.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Telemetry;

// Rejestracja telemetrii w kontenerze: SDK, eksporter (tylko z adresem), magazyn awarii i adapter portu ICrashReporter
[TestFixture]
public sealed class AddTelemetryTests
{
    private TemporaryAppPaths _paths = null!;
    private ServiceProvider? _provider;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
    }

    [TearDown]
    public void TearDown()
    {
        _provider?.Dispose();
        _paths.Dispose();
    }

    private ServiceProvider Build(TelemetryOptions options)
    {
        var services = new ServiceCollection();
        // zależności spoza AddTelemetry: logowanie i katalogi aplikacji (w aplikacji – korzeń kompozycji)
        services.AddLogging();
        services.AddSingleton<IAppPaths>(_paths);
        services.AddTelemetry(options);
        // ZAŁOŻENIE: AddTelemetry rejestruje przekazane TelemetryOptions (potrzebne PendingCrashStore)
        _provider = services.BuildServiceProvider();
        return _provider;
    }

    [Test]
    public void AddTelemetry_Registered_CrashReporterResolvesToOtelCrashReporter()
    {
        var provider = Build(new TelemetryOptions { AppVersion = "1.4.0" });

        var reporter = provider.GetRequiredService<ICrashReporter>();

        Assert.That(reporter, Is.InstanceOf<OtelCrashReporter>());
    }

    [Test]
    public void AddTelemetry_Registered_CrashReporterAndAdapterAreSameSingleton()
    {
        var provider = Build(new TelemetryOptions { AppVersion = "1.4.0" });

        var port = provider.GetRequiredService<ICrashReporter>();
        var adapter = provider.GetRequiredService<OtelCrashReporter>();

        Assert.That(port, Is.SameAs(adapter));
        Assert.That(provider.GetRequiredService<ICrashReporter>(), Is.SameAs(port));
    }

    [Test]
    public void AddTelemetry_Registered_PendingCrashStoreIsSingleton()
    {
        var provider = Build(new TelemetryOptions { AppVersion = "1.4.0" });

        var first = provider.GetRequiredService<PendingCrashStore>();
        var second = provider.GetRequiredService<PendingCrashStore>();

        Assert.That(first, Is.SameAs(second));
    }

    [Test]
    public void AddTelemetry_PendingCrashStoreFromContainer_UsesOptionsAppVersion()
    {
        var provider = Build(new TelemetryOptions { AppVersion = "3.1.4" });
        var store = provider.GetRequiredService<PendingCrashStore>();

        store.Save(new InvalidOperationException("Awaria"));

        Assert.That(store.ReadAll().Single().Crash.AppVersion, Is.EqualTo("3.1.4"));
    }

    [Test]
    public void AddTelemetry_WithoutEndpoint_BuildAndResolveDoNotThrow()
    {
        Assert.DoesNotThrow(() =>
        {
            var provider = Build(new TelemetryOptions { OtlpEndpoint = null, AppVersion = "1.4.0" });
            provider.GetRequiredService<ICrashReporter>();
        });
    }

    [Test]
    public void AddTelemetry_EmptyEndpoint_BuildAndResolveDoNotThrow()
    {
        // pusty adres = bez eksportu (jak null)
        Assert.DoesNotThrow(() =>
        {
            var provider = Build(new TelemetryOptions { OtlpEndpoint = "", AppVersion = "1.4.0" });
            provider.GetRequiredService<ICrashReporter>();
        });
    }

    [Test]
    public void AddTelemetry_WithEndpoint_BuildAndResolveDoNotThrow()
    {
        // eksporter tylko skonfigurowany – nic nie jest wysyłane w teście
        Assert.DoesNotThrow(() =>
        {
            var provider = Build(new TelemetryOptions
            {
                OtlpEndpoint = "http://localhost:4318",
                OtlpHeaders = "Authorization=Basic dGVzdDp0ZXN0",
                AppVersion = "1.4.0"
            });
            provider.GetRequiredService<ICrashReporter>();
        });
    }

    [Test]
    public void TelemetryOptions_Defaults_SamplingAllAndVersionZero()
    {
        var options = new TelemetryOptions();

        Assert.That(options.SamplingRatio, Is.EqualTo(1.0));
        Assert.That(options.AppVersion, Is.EqualTo("0.0.0"));
        Assert.That(options.OtlpEndpoint, Is.Null);
        Assert.That(options.ConsoleExporter, Is.False);
    }

    // Dostawca logów OpenTelemetry w kontenerze – raport awarii może wypchnąć bufor przed usunięciem pliku
    [Test]
    public void AddTelemetry_Registered_LoggerProviderAvailableForFlush()
    {
        var provider = Build(new TelemetryOptions());

        Assert.That(provider.GetService<OpenTelemetry.Logs.LoggerProvider>(), Is.Not.Null);
    }
}
