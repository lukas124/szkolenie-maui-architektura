using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Abstractions;

// Wynik jednego przebiegu wysyłki: ile wysłano, ile czeka, które wizyty zmienił w międzyczasie ktoś inny (Conflicts)
// i których zmian serwer nie przyjął mimo ponowień (Rejected)
public sealed record SyncResult(int Sent, int Pending, IReadOnlyList<VisitId> Conflicts, IReadOnlyList<VisitId> Rejected);

// Port: wysyłka zmian z telefonu na serwer. Kiedy naprawdę się wyśle, decyduje sieć i system – dlatego UI pokazuje,
// co czeka, zamiast obiecywać wysyłkę w tle.
public interface ISyncScheduler
{
    // Prośba o wysyłkę bez czekania; prośby w trakcie przebiegu łączą się w jeden kolejny przebieg
    void RequestSync();

    // Zmiany czekające na wysłanie
    Task<int> GetPendingCountAsync(CancellationToken ct);

    // Po każdym przebiegu (dowolny wątek)
    event EventHandler<SyncResult>? Completed;
}

public static class SyncErrors
{
    // Dyspozytor zmienił wizytę, zanim dotarły zmiany z telefonu – zmiany odrzucone, obowiązuje stan z serwera
    public static readonly Error Conflict = new("Sync.Conflict", ErrorType.Conflict);

    // Serwer nie przyjął zmian wizyty mimo ponowień – zmiany odrzucone, obowiązuje stan z serwera
    public static readonly Error Rejected = new("Sync.Rejected", ErrorType.Unexpected);
}
