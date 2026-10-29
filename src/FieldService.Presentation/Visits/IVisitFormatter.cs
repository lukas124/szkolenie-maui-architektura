using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;

namespace FieldService.Presentation.Visits;

// Wartości → tekst dla użytkownika (kultura i strefa czasowa z ILocalizer, „teraz” z IClock)
public interface IVisitFormatter
{
    // „dziś 10:00–11:30”, „jutro 8:00–9:30”, „sob. 14.11 14:00–15:30”
    string FormatTimeWindow(DateTimeOffset from, DateTimeOffset to);

    // „Zaplanowana”, „Spóźniona”, „W trakcie”, „Zakończona”, „Anulowana”
    string FormatStatus(VisitStatus status, bool isLate);

    // „za 17 min”, gdy wizyta zaczyna się w ciągu godziny; inaczej null
    string? FormatStartsIn(DateTimeOffset from);

    // „Zakończone: 1 z 4 · do zrobienia: 3”
    string FormatDaySummary(IReadOnlyList<VisitSummary> visits);

    // „Aktualizacja: 12:32”
    string FormatUpdatedNow();

    // „Bez sieci – dane z 9:12” (z innego dnia: „Bez sieci – dane z 28.10 9:12”)
    string FormatOfflineSince(DateTimeOffset savedAt);

    // „Oczekuje na wysłanie (2)”
    string FormatPending(int count);
}
