namespace FieldService.Infrastructure.Visits;

// Strona historii w formacie API (GET api/visits/history) – nie wychodzi poza Infrastructure
internal sealed class VisitHistoryPageDto
{
    public List<VisitHistoryItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int TotalCount { get; set; }

    internal sealed class VisitHistoryItemDto
    {
        public Guid Id { get; set; }
        public string WorkOrderNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTimeOffset PlannedStart { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string CustomerName { get; set; } = "";
        public string City { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string? Outcome { get; set; }
        public string ThumbnailUrl { get; set; } = "";
    }
}
