using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>Dane testowe prezentacji: podsumowania wizyt i elementy listy.</summary>
internal static class SpecVisitData
{
    /// <summary>Strefa technika w listopadzie (czas środkowoeuropejski).</summary>
    public static readonly TimeSpan Cet = TimeSpan.FromHours(1);

    /// <summary>Listopad 2026: 12.11 to czwartek, 13.11 piątek, 14.11 sobota.</summary>
    public static DateTimeOffset At(int day, int hour, int minute = 0) =>
        new(2026, 11, day, hour, minute, 0, Cet);

    public static VisitSummary NewVisit(
        DateTimeOffset start,
        VisitStatus status = VisitStatus.Planned,
        string customer = "Jan Kowalski",
        bool isLate = false,
        bool isBrigade = false) =>
        new(
            Id: Guid.NewGuid(),
            WorkOrderNumber: "ZL-2026-0042",
            Status: status,
            PlannedStart: start,
            PlannedEnd: start + TimeSpan.FromMinutes(90),
            CustomerName: customer,
            Address: "ul. Długa 5, Gdańsk",
            DeviceName: "Pompa ciepła X200",
            FaultDescription: "Nie grzeje",
            IsBrigade: isBrigade,
            ThumbnailUrl: "https://example.test/x200.png",
            Version: 3,
            IsLate: isLate);

    public static VisitItem Item(VisitSummary visit) => new()
    {
        Id = visit.Id,
        CustomerName = visit.CustomerName,
        AddressText = visit.Address,
        DeviceName = visit.DeviceName,
        FaultDescription = visit.FaultDescription,
        WorkOrderNumber = visit.WorkOrderNumber,
        TimeWindowText = "okno",
        StatusText = "status",
        Tone = VisitTone.Neutral,
        IsLate = visit.IsLate,
        IsBrigade = visit.IsBrigade,
        IsCancelled = visit.Status == VisitStatus.Cancelled,
        ThumbnailUrl = visit.ThumbnailUrl,
    };
}
