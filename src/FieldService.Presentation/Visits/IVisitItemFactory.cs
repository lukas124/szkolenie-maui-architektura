using FieldService.Application.Visits.Queries;

namespace FieldService.Presentation.Visits;

// Jedno miejsce „dane wizyty → element listy”
public interface IVisitItemFactory
{
    VisitItem Create(VisitSummary visit);

    // Wpis historii → wiersz; open – co zrobić po dotknięciu wiersza
    VisitHistoryItem CreateHistory(VisitHistoryEntry entry, Func<Task> open);
}
