namespace FieldService.Infrastructure.Visits;

// Szczegóły wizyty w formacie API (GET api/visits/{id}) – nie wychodzą poza Infrastructure
internal sealed class VisitDetailsDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset PlannedStart { get; set; }
    public DateTimeOffset PlannedEnd { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public CustomerDto Customer { get; set; } = new();
    public string DeviceName { get; set; } = "";
    public string FaultDescription { get; set; } = "";
    public List<string> TechnicianEmails { get; set; } = [];
    public List<ActivityDto> Activities { get; set; } = [];
    public List<UsedPartDto> Parts { get; set; } = [];
    public int ScopeVersion { get; set; }
    public SignatureDto? Signature { get; set; }
    public string? Outcome { get; set; }
    public string? OutcomeReason { get; set; }
    public string? CancelReason { get; set; }
    public string ThumbnailUrl { get; set; } = "";
    public int Version { get; set; }

    internal sealed class CustomerDto
    {
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
    }

    internal sealed class ActivityDto
    {
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTimeOffset RecordedAt { get; set; }
    }

    internal sealed class UsedPartDto
    {
        public string PartId { get; set; } = "";
        public int Quantity { get; set; }
    }

    internal sealed class SignatureDto
    {
        public string SignerName { get; set; } = "";
        public DateTimeOffset SignedAt { get; set; }
        public int ScopeVersion { get; set; }
    }
}
