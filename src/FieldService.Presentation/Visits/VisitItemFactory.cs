using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Visits;

// Fabryka: VisitSummary (model odczytu) → VisitItem (model prezentacji) – teksty z formattera, ton ze statusu
public sealed class VisitItemFactory(IVisitFormatter formatter, ILocalizer localizer) : IVisitItemFactory
{
    public VisitItem Create(VisitSummary visit)
    {
        var (statusKey, tone) = visit.Status switch
        {
            VisitStatus.Planned when visit.IsLate => ("Visits_Status_Late", VisitTone.Danger),
            VisitStatus.Planned => ("Visits_Status_Planned", VisitTone.Neutral),
            VisitStatus.InProgress => ("Visits_Status_InProgress", VisitTone.Info),
            VisitStatus.Completed => ("Visits_Status_Completed", VisitTone.Success),
            _ => ("Visits_Status_Cancelled", VisitTone.Warning)
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
            StatusText = localizer[statusKey],
            Tone = tone,
            IsLate = visit.IsLate,
            IsBrigade = visit.IsBrigade,
            IsCancelled = visit.Status == VisitStatus.Cancelled,
            ThumbnailUrl = visit.ThumbnailUrl
        };
    }
}
