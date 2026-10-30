using FieldService.Application.Abstractions;
using FieldService.Infrastructure.Telemetry;

namespace FieldService.UI.Telemetry;

// Zdarzenia awarii platformy podpięte w korzeniu kompozycji (MauiProgram). Awaria nieobsłużona → plik (PendingCrashStore),
// wysyłka przy następnym starcie. Wyjątek zgubiony w zadaniu (FireAndForget, nieobserwowany Task) → raport obsłużony.
public static class CrashHandlers
{
    private static PendingCrashStore? _store;
    private static ICrashReporter? _reporter;
    private static int _saved; // awaria zapisana – to samo zdarzenie potrafi przyjść dwiema drogami (platforma i AppDomain)

    public static void Register(IServiceProvider services)
    {
        _store = services.GetRequiredService<PendingCrashStore>();
        _reporter = services.GetRequiredService<ICrashReporter>();

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Save(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportHandled(e.Exception);
            e.SetObserved();
        };
#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => Save(e.Exception);
#elif IOS
        ObjCRuntime.Runtime.MarshalManagedException += (_, e) => Save(e.Exception);
#endif
    }

    // wyjątek, po którym aplikacja działa dalej (np. z FireAndForget)
    public static void ReportHandled(Exception exception) => _reporter?.RecordHandled(exception);

    private static void Save(Exception? exception)
    {
        // pierwsza awaria w procesie – po niej proces i tak się kończy
        if (exception is null || Interlocked.Exchange(ref _saved, 1) == 1) return;
        _store?.Save(exception);
    }
}
