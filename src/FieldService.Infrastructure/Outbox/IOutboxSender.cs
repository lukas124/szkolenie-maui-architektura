using FieldService.Domain.Common;

namespace FieldService.Infrastructure.Outbox;

// Wysyłka jednej wiadomości na serwer (implementuje ApiVisitsGateway). expectedVersion = wersja wizyty, którą widział
// telefon; wynik = nowa wersja wizyty na serwerze.
public interface IOutboxSender
{
    Task<Result<int>> SendAsync(OutboxMessage message, int expectedVersion, CancellationToken ct);
}
