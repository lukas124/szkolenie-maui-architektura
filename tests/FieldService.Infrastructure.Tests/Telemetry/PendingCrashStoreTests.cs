using FieldService.Infrastructure.Telemetry;
using FieldService.Infrastructure.Tests.Persistence;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Telemetry;

// Awaria nieobsłużona: synchroniczny zapis małego pliku JSON w {DataDirectory}/crashes, odczyt przy następnym starcie
[TestFixture]
public sealed class PendingCrashStoreTests
{
    private TemporaryAppPaths _paths = null!;

    [SetUp]
    public void SetUp()
    {
        // ZAŁOŻENIE: TemporaryAppPaths ma konstruktor bez parametrów
        _paths = new TemporaryAppPaths();
    }

    [TearDown]
    public void TearDown()
    {
        _paths.Dispose();
    }

    private string CrashesDirectory => Path.Combine(_paths.DataDirectory, "crashes");

    private PendingCrashStore CreateStore(string appVersion = "1.4.0") =>
        new(_paths, new TelemetryOptions { AppVersion = appVersion });

    // wyjątek z prawdziwym stosem (rzucony i złapany)
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
    public void ReadAll_NoCrashesSaved_ReturnsEmpty()
    {
        var store = CreateStore();

        Assert.That(store.ReadAll(), Is.Empty);
    }

    [Test]
    public void Save_Exception_ReadAllReturnsCrashWithTypeMessageAndStackTrace()
    {
        var store = CreateStore();
        var exception = Thrown("Wymuszona awaria");

        store.Save(exception);
        var all = store.ReadAll();

        Assert.That(all, Has.Count.EqualTo(1));
        var crash = all[0].Crash;
        Assert.That(crash.Type, Is.EqualTo("System.InvalidOperationException"));
        Assert.That(crash.Message, Is.EqualTo("Wymuszona awaria"));
        // StackTrace = exception.ToString() (typ, komunikat i stos)
        Assert.That(crash.StackTrace, Is.EqualTo(exception.ToString()));
        Assert.That(crash.StackTrace, Does.Contain(nameof(Thrown)));
    }

    [Test]
    public void Save_Exception_AppVersionComesFromOptions()
    {
        var store = CreateStore(appVersion: "2.7.1");

        store.Save(Thrown("Awaria"));

        Assert.That(store.ReadAll()[0].Crash.AppVersion, Is.EqualTo("2.7.1"));
    }

    [Test]
    public void Save_Exception_OccurredAtIsCurrentUtcTime()
    {
        var store = CreateStore();
        // brak zegara w kontrakcie (OccurredAt = teraz) – sprawdzamy przedział wokół zapisu
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        store.Save(Thrown("Awaria"));

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        var occurredAt = store.ReadAll()[0].Crash.OccurredAt;
        Assert.That(occurredAt, Is.InRange(before, after));
        Assert.That(occurredAt.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Save_Exception_WritesFileInCrashesDirectory()
    {
        var store = CreateStore();

        store.Save(Thrown("Awaria"));

        var path = store.ReadAll()[0].Path;
        Assert.That(File.Exists(path), Is.True);
        Assert.That(Path.GetFullPath(Path.GetDirectoryName(path)!), Is.EqualTo(Path.GetFullPath(CrashesDirectory)));
    }

    [Test]
    public void Save_CrashFromNewStoreInstance_IsReadAfterRestart()
    {
        // „następny start” – nowy obiekt na tym samym katalogu
        CreateStore().Save(Thrown("Awaria przed restartem"));

        var afterRestart = CreateStore().ReadAll();

        Assert.That(afterRestart, Has.Count.EqualTo(1));
        Assert.That(afterRestart[0].Crash.Message, Is.EqualTo("Awaria przed restartem"));
    }

    [Test]
    public void ReadAll_TwoCrashes_ReturnsOldestFirst()
    {
        var store = CreateStore();

        store.Save(Thrown("pierwsza"));
        // odstęp, żeby chwile zapisu się różniły
        Thread.Sleep(20);
        store.Save(Thrown("druga"));

        var all = store.ReadAll();

        Assert.That(all.Select(c => c.Crash.Message), Is.EqualTo(new[] { "pierwsza", "druga" }));
    }

    [Test]
    public void ReadAll_CorruptedFile_IsSkippedAndDeleted()
    {
        var store = CreateStore();
        store.Save(Thrown("poprawna"));
        // ZAŁOŻENIE: pliki awarii mają rozszerzenie .json
        var corrupted = Path.Combine(CrashesDirectory, "zepsuty.json");
        File.WriteAllText(corrupted, "{ to nie jest JSON");

        var all = store.ReadAll();

        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Crash.Message, Is.EqualTo("poprawna"));
        Assert.That(File.Exists(corrupted), Is.False);
    }

    [Test]
    public void Delete_SavedCrash_FileRemovedAndNotReadAgain()
    {
        var store = CreateStore();
        store.Save(Thrown("Awaria"));
        var path = store.ReadAll()[0].Path;

        store.Delete(path);

        Assert.That(File.Exists(path), Is.False);
        Assert.That(store.ReadAll(), Is.Empty);
    }

    [Test]
    public void Delete_OneOfTwoCrashes_OtherRemains()
    {
        var store = CreateStore();
        store.Save(Thrown("pierwsza"));
        Thread.Sleep(20);
        store.Save(Thrown("druga"));
        var first = store.ReadAll()[0];

        store.Delete(first.Path);

        var remaining = store.ReadAll();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].Crash.Message, Is.EqualTo("druga"));
    }

    [Test]
    public void Save_CrashesDirectoryUnavailable_DoesNotThrow()
    {
        // w miejscu katalogu crashes jest zwykły plik – katalogu nie da się utworzyć
        Directory.CreateDirectory(_paths.DataDirectory);
        File.WriteAllText(CrashesDirectory, "to nie katalog");
        var store = CreateStore();

        // awaria w trakcie awarii – Save nigdy nie rzuca
        Assert.DoesNotThrow(() => store.Save(Thrown("Awaria")));
    }
}
