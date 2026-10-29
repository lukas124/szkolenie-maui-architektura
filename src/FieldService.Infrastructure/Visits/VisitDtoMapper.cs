using FieldService.Application.Visits.Queries;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;

namespace FieldService.Infrastructure.Visits;

// Warstwa ochronna (ACL): format API → model odczytu i model domeny aplikacji
internal static class VisitDtoMapper
{
    public static VisitSummary ToSummary(VisitDto dto) => new(
        dto.Id,
        dto.WorkOrderNumber,
        ToStatus(dto.Status),
        dto.PlannedStart,
        dto.PlannedEnd,
        dto.CustomerName,
        dto.Address,
        dto.DeviceName,
        dto.FaultDescription,
        dto.IsBrigade,
        dto.ThumbnailUrl,
        dto.Version);

    public static RemoteVisit ToRemoteVisit(VisitDetailsDto dto)
    {
        var scope = new VisitScope(
            dto.Activities.Select(a => new Activity(a.Code, a.Description, a.RecordedAt)).ToList(),
            dto.Parts.Select(p => new UsedPart(new PartId(p.PartId), Quantity.Create(p.Quantity).Value)).ToList());
        var visit = Visit.Restore(
            new VisitId(dto.Id),
            dto.WorkOrderId,
            dto.TechnicianEmails,
            ToStatus(dto.Status),
            dto.PlannedStart,
            dto.PlannedEnd,
            dto.StartedAt,
            dto.CompletedAt,
            scope,
            dto.ScopeVersion,
            dto.Signature is null ? null : CustomerSignature.Restore(dto.Signature.SignerName, dto.Signature.SignedAt, dto.Signature.ScopeVersion),
            dto.Outcome is null ? null : VisitOutcome.Create(Enum.Parse<VisitOutcomeType>(dto.Outcome), dto.OutcomeReason).Value,
            dto.CancelReason,
            dto.Version);
        var snapshot = new VisitSnapshot(
            dto.WorkOrderNumber,
            new CustomerSnapshot(dto.Customer.Name, dto.Customer.Phone, $"{dto.Customer.Street}, {dto.Customer.City}"),
            dto.DeviceName,
            dto.FaultDescription,
            dto.ThumbnailUrl);
        return new RemoteVisit(visit, snapshot);
    }

    private static VisitStatus ToStatus(string status) =>
        Enum.TryParse<VisitStatus>(status, ignoreCase: true, out var value) ? value : VisitStatus.Planned;
}
