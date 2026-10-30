using FieldService.Application.Visits.Queries;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using Dto = FieldService.Infrastructure.Api;

namespace FieldService.Infrastructure.Visits;

// Warstwa ochronna (ACL): DTO wygenerowanego klienta → model odczytu i model domeny aplikacji.
// DTO nie wychodzą poza Infrastructure.
internal static class VisitDtoMapper
{
    public static VisitSummary ToSummary(Dto.VisitSummaryDto dto) => new(
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

    public static RemoteVisit ToRemoteVisit(Dto.VisitDetailsDto dto)
    {
        var scope = new VisitScope(
            dto.Activities.Select(a => new Activity(a.Code, a.Description, a.RecordedAt)).ToList(),
            dto.Parts.Select(p => new UsedPart(new PartId(p.PartId), Quantity.Create(p.Quantity).Value)).ToList());
        var visit = Visit.Restore(
            new VisitId(dto.Id),
            dto.WorkOrderId,
            dto.TechnicianEmails.ToList(),
            ToStatus(dto.Status),
            dto.PlannedStart,
            dto.PlannedEnd,
            dto.StartedAt,
            dto.CompletedAt,
            scope,
            dto.ScopeVersion,
            dto.Signature is null ? null : CustomerSignature.Restore(dto.Signature.SignerName, dto.Signature.SignedAt, dto.Signature.ScopeVersion),
            dto.Outcome is { } outcome ? VisitOutcome.Create(ToOutcome(outcome), dto.OutcomeReason).Value : null,
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

    public static VisitHistoryPageResult ToHistoryPage(Dto.VisitHistoryItemDtoPageDto dto) => new(
        dto.Items.Select(i => new VisitHistoryEntry(
            i.Id,
            i.WorkOrderNumber,
            ToStatus(i.Status),
            i.PlannedStart,
            i.CompletedAt,
            i.CustomerName,
            i.City,
            i.DeviceName,
            i.Outcome is { } outcome ? ToOutcome(outcome) : null,
            i.ThumbnailUrl,
            // pełny obraz (w liście nie używamy – pułapka startu ćwiczenia 12): ten sam adres bez parametru miniatury
            i.ThumbnailUrl.Replace("?size=thumb", "", StringComparison.Ordinal))).ToList(),
        dto.Page,
        dto.TotalCount);

    // wyliczenia API i domeny mają te same nazwy – po nazwie, nie po liczbie
    private static VisitStatus ToStatus(Dto.VisitStatus status) => Enum.Parse<VisitStatus>(status.ToString());

    private static VisitOutcomeType ToOutcome(Dto.VisitOutcomeType outcome) => Enum.Parse<VisitOutcomeType>(outcome.ToString());
}
