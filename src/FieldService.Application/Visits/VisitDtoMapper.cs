using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits;

// Warstwa ochronna (ACL): format API → model odczytu aplikacji
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

    private static VisitStatus ToStatus(string status) =>
        Enum.TryParse<VisitStatus>(status, ignoreCase: true, out var value) ? value : VisitStatus.Planned;
}
