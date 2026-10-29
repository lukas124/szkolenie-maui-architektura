using FieldService.UI.Models;

namespace FieldService.UI.Services;

// Ostatnio pobrane wizyty – szczegóły wizyty pokazują się od razu, zanim przyjdzie odpowiedź z API
public interface IVisitStore
{
    void SetVisits(IEnumerable<VisitDto> visits);
    VisitDto? Find(Guid id);
}

public class VisitStore : IVisitStore
{
    private readonly Dictionary<Guid, VisitDto> _visits = new();

    public void SetVisits(IEnumerable<VisitDto> visits)
    {
        _visits.Clear();
        foreach (var visit in visits) _visits[visit.Id] = visit;
    }

    public VisitDto? Find(Guid id) => _visits.GetValueOrDefault(id);
}
