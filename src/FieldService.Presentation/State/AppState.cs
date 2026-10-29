using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;

namespace FieldService.Presentation.State;

// Singleton w kontenerze (jedna instancja wstrzykiwana), nie singleton GoF ze statycznym Instance
public sealed class AppState : IAppState
{
    private const string OverdueKey = "visits_filter_overdue";
    private const string StatusKey = "visits_filter_status";

    private readonly IPreferencesStore _preferences;

    public AppState(IPreferencesStore preferences)
    {
        _preferences = preferences;
        Filter = new VisitFilterState
        {
            OnlyOverdue = preferences.Get(OverdueKey) == "true",
            Status = Enum.TryParse<VisitStatus>(preferences.Get(StatusKey), out var status) ? status : null
        };
    }

    public VisitFilterState Filter { get; }

    public void ApplyFilter(bool onlyOverdue, VisitStatus? status)
    {
        Filter.OnlyOverdue = onlyOverdue;
        Filter.Status = status;
        _preferences.Set(OverdueKey, onlyOverdue ? "true" : "false");
        _preferences.Set(StatusKey, status?.ToString() ?? "");
    }
}
