using System.Globalization;
using System.Text.Json;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;

namespace FieldService.Infrastructure.Persistence;

// Data Mapper (Fowler): Visit ↔ wiersz bazy. Zakres i podpis jako JSON w jednym wierszu (jeden odczyt, jeden zapis).
internal static class VisitRowMapper
{
    private sealed record ScopeData(List<ActivityData> Activities, List<PartData> Parts);
    private sealed record ActivityData(string Code, string Description, string RecordedAt);
    private sealed record PartData(string PartId, int Quantity);
    private sealed record SignatureData(string SignerName, string SignedAt, int ScopeVersion);

    // Stan agregatu → kolumny wiersza (migawki nie dotyka)
    public static void WriteState(Visit visit, VisitRow row)
    {
        row.Id = visit.Id.Value.ToString();
        row.WorkOrderId = visit.WorkOrderId.ToString();
        row.TechnicianIdsJson = JsonSerializer.Serialize(visit.TechnicianIds);
        row.Status = visit.Status.ToString();
        row.PlannedStart = Format(visit.PlannedStart);
        row.PlannedEnd = Format(visit.PlannedEnd);
        row.StartedAt = Format(visit.StartedAt);
        row.CompletedAt = Format(visit.CompletedAt);
        row.ScopeJson = JsonSerializer.Serialize(new ScopeData(
            visit.Scope.Activities.Select(a => new ActivityData(a.Code, a.Description, Format(a.RecordedAt))).ToList(),
            visit.Scope.Parts.Select(p => new PartData(p.PartId.Value, p.Quantity.Value)).ToList()));
        row.ScopeVersion = visit.ScopeVersion;
        row.SignatureJson = visit.Signature is null
            ? null
            : JsonSerializer.Serialize(new SignatureData(visit.Signature.SignerName, Format(visit.Signature.SignedAt), visit.Signature.ScopeVersion));
        row.OutcomeType = visit.Outcome?.Type.ToString();
        row.OutcomeReason = visit.Outcome?.Reason;
        row.CancelReason = visit.CancelReason;
        row.Version = visit.Version;
    }

    public static Visit ToVisit(VisitRow row)
    {
        var scope = JsonSerializer.Deserialize<ScopeData>(row.ScopeJson)!;
        var signature = row.SignatureJson is null ? null : JsonSerializer.Deserialize<SignatureData>(row.SignatureJson)!;
        return Visit.Restore(
            new VisitId(Guid.Parse(row.Id)),
            Guid.Parse(row.WorkOrderId),
            JsonSerializer.Deserialize<List<string>>(row.TechnicianIdsJson)!,
            Enum.Parse<VisitStatus>(row.Status),
            Parse(row.PlannedStart),
            Parse(row.PlannedEnd),
            ParseNullable(row.StartedAt),
            ParseNullable(row.CompletedAt),
            new VisitScope(
                scope.Activities.Select(a => new Activity(a.Code, a.Description, Parse(a.RecordedAt))).ToList(),
                scope.Parts.Select(p => new UsedPart(new PartId(p.PartId), Quantity.Create(p.Quantity).Value)).ToList()),
            row.ScopeVersion,
            signature is null ? null : CustomerSignature.Restore(signature.SignerName, Parse(signature.SignedAt), signature.ScopeVersion),
            row.OutcomeType is null ? null : VisitOutcome.Create(Enum.Parse<VisitOutcomeType>(row.OutcomeType), row.OutcomeReason).Value,
            row.CancelReason,
            row.Version);
    }

    public static void WriteSnapshot(VisitSnapshot snapshot, VisitRow row)
    {
        row.WorkOrderNumber = snapshot.WorkOrderNumber;
        row.CustomerName = snapshot.Customer.Name;
        row.CustomerPhone = snapshot.Customer.Phone;
        row.Address = snapshot.Customer.Address;
        row.DeviceName = snapshot.DeviceName;
        row.FaultDescription = snapshot.FaultDescription;
        row.ThumbnailUrl = snapshot.ThumbnailUrl;
    }

    public static VisitDetails ToDetails(VisitRow row)
    {
        var visit = ToVisit(row);
        return new VisitDetails(
            visit.Id.Value,
            row.WorkOrderNumber,
            visit.Status,
            visit.PlannedStart,
            visit.PlannedEnd,
            new CustomerSnapshot(row.CustomerName, row.CustomerPhone, row.Address),
            row.DeviceName,
            row.FaultDescription,
            row.ThumbnailUrl,
            visit.Scope.Activities.Select(a => a.Description).ToList(),
            visit.Scope.Parts.Select(p => new VisitPartLine(p.PartId.Value, p.Quantity.Value)).ToList(),
            visit.HasValidSignature(),
            visit.Signature?.SignerName,
            visit.Outcome?.Type,
            PhotoCount: 0);
    }

    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static string? Format(DateTimeOffset? value) => value is null ? null : Format(value.Value);

    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseNullable(string? value) => value is null ? null : Parse(value);
}
