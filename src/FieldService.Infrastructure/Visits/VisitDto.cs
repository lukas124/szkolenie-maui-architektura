namespace FieldService.Infrastructure.Visits;

// Wizyta na dziś w formacie API (GET api/visits/today) – nie wychodzi poza Infrastructure
internal sealed class VisitDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset PlannedStart { get; set; }
    public DateTimeOffset PlannedEnd { get; set; }
    public string CustomerName { get; set; } = "";
    public string Address { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string FaultDescription { get; set; } = "";
    public bool IsBrigade { get; set; }
    public string ThumbnailUrl { get; set; } = "";
    public int Version { get; set; }
}
