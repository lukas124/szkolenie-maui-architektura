using FieldService.Application.Visits.Queries;

namespace FieldService.Presentation.Visits;

// Jedno miejsce „dane wizyty → element listy”
public interface IVisitItemFactory
{
    VisitItem Create(VisitSummary visit);
}
