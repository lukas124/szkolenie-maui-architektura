using FieldService.Application.Visits.Queries;

namespace FieldService.Presentation.Visits;

// Wartości → tekst dla użytkownika (kultura i strefa czasowa z ILocalizer, „teraz” z IClock)
public interface IVisitFormatter
{
    // „dziś 10:00–11:30”, „jutro 8:00–9:30”, „sob. 14.11 14:00–15:30”
    string FormatTimeWindow(DateTimeOffset from, DateTimeOffset to);

    // „za 17 min”, gdy wizyta zaczyna się w ciągu godziny; inaczej null
    string? FormatStartsIn(DateTimeOffset from);

    // „Zakończone: 1 z 4 · do zrobienia: 3”
    string FormatDaySummary(IReadOnlyList<VisitSummary> visits);

    // „Aktualizacja: 12:32”
    string FormatUpdatedNow();
}
