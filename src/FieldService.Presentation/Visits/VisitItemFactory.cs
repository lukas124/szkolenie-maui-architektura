using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;

namespace FieldService.Presentation.Visits;

// Fabryka: VisitSummary (model odczytu) → VisitItem (model prezentacji) – teksty z formattera, ton ze statusu
public sealed class VisitItemFactory(IVisitFormatter formatter) : IVisitItemFactory
{
    public VisitItem Create(VisitSummary visit)
    {
        var tone = visit.Status switch
        {
            VisitStatus.Planned when visit.IsLate => VisitTone.Danger,
            VisitStatus.Planned => VisitTone.Neutral,
            VisitStatus.InProgress => VisitTone.Info,
            VisitStatus.Completed => VisitTone.Success,
            _ => VisitTone.Warning
        };

        return new VisitItem
        {
            Id = visit.Id,
            CustomerName = visit.CustomerName,
            AddressText = visit.Address,
            DeviceName = visit.DeviceName,
            FaultDescription = visit.FaultDescription,
            WorkOrderNumber = visit.WorkOrderNumber,
            TimeWindowText = formatter.FormatTimeWindow(visit.PlannedStart, visit.PlannedEnd),
            StartsInText = visit.Status == VisitStatus.Planned ? formatter.FormatStartsIn(visit.PlannedStart) : null,
            StatusText = formatter.FormatStatus(visit.Status, visit.IsLate),
            Tone = tone,
            IsLate = visit.IsLate,
            IsBrigade = visit.IsBrigade,
            IsCancelled = visit.Status == VisitStatus.Cancelled,
            ThumbnailUrl = visit.ThumbnailUrl
        };
    }
}
