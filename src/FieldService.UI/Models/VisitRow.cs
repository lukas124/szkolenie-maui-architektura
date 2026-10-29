using SQLite;

namespace FieldService.UI.Models;

// Wizyta zapisana w telefonie – lista działa bez internetu
[Table("visits")]
public class VisitRow
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string WorkOrderNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public string CustomerName { get; set; } = "";
    public string Address { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public int Version { get; set; }
}

// „Rozpocznij” zapisane bez internetu – czeka na wysłanie
[Table("pending_starts")]
public class PendingStart
{
    [PrimaryKey]
    public string VisitId { get; set; } = "";
    public int Version { get; set; }
    public DateTime StartedAt { get; set; }
}
