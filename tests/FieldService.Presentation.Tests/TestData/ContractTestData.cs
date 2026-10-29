using System.Globalization;
using FieldService.Application.Orders.Queries;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Tests.Contract;

// Wspólne dane testowe checkpointu 10. Jedyne miejsce zależne od konstruktora VisitSummary
// i od sposobu tworzenia Result<T> (oba spoza kontraktu 09/10 – patrz PYTANIA.md).
public static class ContractTestData
{
    public static VisitSummary Summary(Guid id, VisitStatus status, DateTimeOffset plannedStart) =>
        new(id, "ZL/2026/" + id.ToString()[..4], status, plannedStart, plannedStart.AddHours(1), "Klient",
            "ul. Testowa 1, Warszawa", "Kocioł", "Nie grzeje", false, "https://example.invalid/thumb.png", 1);

    public static WorkOrderSummary WorkOrder(string number, WorkOrderStatus status, DateTimeOffset slaDueAt) =>
        new(Guid.NewGuid(), number, status, "Klient " + number, "ul. Testowa 1", "Kocioł", slaDueAt, 1);

    public static VisitDetails Details(VisitId id, DateTimeOffset plannedStart, IReadOnlyList<string> activities) =>
        new(id.Value, "ZL/2026/0001", VisitStatus.InProgress, plannedStart, plannedStart.AddHours(2),
            new CustomerSnapshot("Jan Kowalski", "+48 600 000 000", "ul. Testowa 1, Warszawa"),
            "Kocioł gazowy", "Nie grzeje wody", "https://example.invalid/thumb.png",
            activities, Array.Empty<VisitPartLine>(), false, null, null, 0);

    public static Result<T> Ok<T>(T value) => Result<T>.Success(value);

    public static Result<T> Fail<T>(Error error) => Result<T>.Failure(error);

    public static Error NetworkError() => new("Network.Unavailable", ErrorType.Network);

    // Lokalizator, który zwraca sam klucz (jak przy braku tłumaczenia)
    public static Mock<ILocalizer> KeyLocalizer()
    {
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        localizer.Setup(l => l.CurrentCulture).Returns(CultureInfo.InvariantCulture);
        return localizer;
    }

    // Czeka na efekt obsługi komunikatu (odbiorca może przeładować listę bez await)
    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
