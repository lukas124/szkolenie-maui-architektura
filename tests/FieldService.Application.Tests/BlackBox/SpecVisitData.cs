using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;

namespace FieldService.Application.Tests.BlackBox;

/// <summary>Dane testowe: podsumowania wizyt z czytelnymi wartościami domyślnymi.</summary>
internal static class SpecVisitData
{
    /// <summary>Strefa technika w listopadzie (czas środkowoeuropejski).</summary>
    public static readonly TimeSpan Cet = TimeSpan.FromHours(1);

    public static DateTimeOffset At(int day, int hour, int minute = 0) =>
        new(2026, 11, day, hour, minute, 0, Cet);

    public static VisitSummary NewVisit(
        DateTimeOffset start,
        VisitStatus status = VisitStatus.Planned,
        string customer = "Jan Kowalski",
        TimeSpan? duration = null,
        bool isLate = false) =>
        new(
            Id: Guid.NewGuid(),
            WorkOrderNumber: "ZL-2026-0042",
            Status: status,
            PlannedStart: start,
            PlannedEnd: start + (duration ?? TimeSpan.FromMinutes(90)),
            CustomerName: customer,
            Address: "ul. Długa 5, Gdańsk",
            DeviceName: "Pompa ciepła X200",
            FaultDescription: "Nie grzeje",
            IsBrigade: false,
            ThumbnailUrl: "https://example.test/x200.png",
            Version: 3,
            IsLate: isLate);
}
