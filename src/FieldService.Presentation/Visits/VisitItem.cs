using Microsoft.Maui.Graphics;

namespace FieldService.Presentation.Visits;

// Element listy wizyt – dane gotowe do wyświetlenia
public sealed class VisitItem
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string TimeText { get; init; }
    public required string StatusText { get; init; }
    // TODO: Ćwiczenie 02 – Color to typ MAUI; zamień na VisitTone Tone (kolor dobierze ToneToColorConverter w UI)
    public required Color StatusColor { get; init; }
    public required string ThumbnailUrl { get; init; }
    public required bool IsBrigade { get; init; }
    public required bool IsCancelled { get; init; }

    // szczegóły wizyty (prosty ekran do Bloku 07)
    public required string WorkOrderNumber { get; init; }
    public required string Address { get; init; }
    public required string DeviceName { get; init; }
    public required string FaultDescription { get; init; }
}
