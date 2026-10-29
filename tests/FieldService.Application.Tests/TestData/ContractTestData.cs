using FieldService.Application.Orders.Queries;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Tests.Contract;

// Wspólne dane testowe checkpointu 10. Jedyne miejsce zależne od konstruktora VisitSummary
// i od sposobu tworzenia Result<T> (oba spoza kontraktu 09/10 – patrz PYTANIA.md).
public static class ContractTestData
{
    public static VisitSummary Summary(Guid id, VisitStatus status, DateTimeOffset plannedStart) =>
        new(id, "ZL/2026/" + id.ToString()[..4], status, plannedStart, plannedStart.AddHours(1), "Klient",
            "ul. Testowa 1, Warszawa", "Kocioł", "Nie grzeje", false, "https://example.invalid/thumb.png", 1);

    public static WorkOrderSummary WorkOrder(string number, WorkOrderStatus status, DateTimeOffset slaDueAt) =>
        new(Guid.NewGuid(), number, status, "Klient " + number, "ul. Testowa 1", "Kocioł", slaDueAt, 1);

    public static Result<T> Ok<T>(T value) => Result<T>.Success(value);

    public static Result<T> Fail<T>(Error error) => Result<T>.Failure(error);

    public static Error NetworkError() => new("Network.Unavailable", ErrorType.Network);
}
