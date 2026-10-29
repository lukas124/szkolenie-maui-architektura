using CommunityToolkit.Mvvm.Input;

namespace FieldService.Presentation.Visits;

// Model prezentacji wiersza historii. Komenda w modelu elementu: wiersz w XAML wiąże się z własnym OpenCommand,
// bez szukania ViewModelu strony (RelativeSource) w każdym wierszu.
public sealed class VisitHistoryItem
{
    public required Guid Id { get; init; }
    public required string CustomerName { get; init; }
    public required string DeviceName { get; init; }
    public required string City { get; init; }
    public required string WorkOrderNumber { get; init; }
    public required string StatusText { get; init; }
    public required VisitTone Tone { get; init; }
    public bool IsCancelled { get; init; }

    // data wizyty; „3 dni temu” robi konwerter w UI
    public required DateTimeOffset VisitDate { get; init; }
    public required string ThumbnailUrl { get; init; }
    // pełne zdjęcie – nie do listy (w liście miniatura ThumbnailUrl)
    public required string PhotoUrl { get; init; }
    public required IAsyncRelayCommand OpenCommand { get; init; }
}
