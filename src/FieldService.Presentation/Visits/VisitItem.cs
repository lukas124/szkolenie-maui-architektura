namespace FieldService.Presentation.Visits;

// Model prezentacji (Presentation Model, Fowler): gotowe teksty i flagi pod ekran, bez reguł i bez typów MAUI
public sealed record VisitItem
{
    public required Guid Id { get; init; }
    public required string CustomerName { get; init; }
    public required string AddressText { get; init; }
    public required string DeviceName { get; init; }
    public required string FaultDescription { get; init; }
    public required string WorkOrderNumber { get; init; }
    public required string TimeWindowText { get; init; }
    public string? StartsInText { get; init; }
    public required string StatusText { get; init; }
    public required VisitTone Tone { get; init; }
    public required bool IsLate { get; init; }
    public required bool IsBrigade { get; init; }
    public required bool IsCancelled { get; init; }
    public required string ThumbnailUrl { get; init; }
}
