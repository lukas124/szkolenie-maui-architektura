using CommunityToolkit.Mvvm.Messaging;
using FieldService.Application.Abstractions;
using FieldService.Presentation.Messages;

namespace FieldService.UI.Platform;

// Wyzwalacze wysyłki zależne od platformy: powrót aplikacji z tła i odzyskanie internetu → prośba o wysyłkę.
// Praca w tle (WorkManager, BGTaskScheduler) jest w rękach systemu – wysyłamy, gdy aplikacja działa.
// Wynik przebiegu przychodzi z dowolnego wątku – do ViewModeli trafia komunikatem na wątku UI.
public sealed class SyncTriggers
{
    private readonly ISyncScheduler _sync;

    public SyncTriggers(ISyncScheduler sync)
    {
        _sync = sync;
        Connectivity.Current.ConnectivityChanged += (_, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet) _sync.RequestSync();
        };
        _sync.Completed += (_, result) =>
            MainThread.BeginInvokeOnMainThread(() => WeakReferenceMessenger.Default.Send(new SyncCompletedMessage(result)));
    }

    // Okno może powstać więcej niż raz w czasie życia procesu (Android) – każde dostaje wyzwalacz powrotu z tła
    public void Attach(Window window) => window.Resumed += (_, _) => _sync.RequestSync();
}
