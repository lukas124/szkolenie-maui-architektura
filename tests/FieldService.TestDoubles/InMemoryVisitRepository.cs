using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.TestDoubles;

// Fałszywka repozytorium wizyt (LSP): zachowuje się jak prawdziwe repozytorium.
// - nieznane Id → null,
// - przechowuje KOPIĘ stanu: zmiana obiektu bez SaveAsync nie jest widoczna w magazynie,
// - SaveAsync z inną wersją niż zapisana → VisitErrors.VersionConflict (nic nie zapisano),
// - sukces → Version + 1 i wyczyszczone DomainEvents.
public sealed class InMemoryVisitRepository : IVisitRepository
{
    private readonly Dictionary<VisitId, Visit> _store = new();
    private readonly List<IDomainEvent> _savedEvents = [];

    // Liczba udanych zapisów (SaveAsync) – test sprawdza, czy handler zapisał.
    public int SaveCount { get; private set; }

    // Zdarzenia, które były na agregacie w chwili udanych zapisów (w kolejności).
    public IReadOnlyList<IDomainEvent> SavedEvents => _savedEvents;

    public Task<Visit?> GetAsync(VisitId id, CancellationToken ct) =>
        Task.FromResult<Visit?>(_store.TryGetValue(id, out var stored) ? Copy(stored) : null);

    public Task AddAsync(Visit visit, CancellationToken ct)
    {
        Add(visit);
        return Task.CompletedTask;
    }

    public Task<Result> SaveAsync(Visit visit, CancellationToken ct)
    {
        if (!_store.TryGetValue(visit.Id, out var stored) || stored.Version != visit.Version)
        {
            return Task.FromResult(Result.Failure(VisitErrors.VersionConflict));
        }

        _savedEvents.AddRange(visit.DomainEvents);
        visit.IncrementVersion();
        visit.ClearDomainEvents();
        _store[visit.Id] = Copy(visit);
        SaveCount++;
        return Task.FromResult(Result.Success());
    }

    public Task<bool> HasOtherInProgressAsync(VisitId visitId, CancellationToken ct) =>
        Task.FromResult(_store.Values.Any(v => v.Id != visitId && v.Status == VisitStatus.InProgress));

    // Pomocnicze dla sekcji „given”: wizyta w replice bez liczenia zapisu.
    public void Add(Visit visit) => _store[visit.Id] = Copy(visit);

    public bool Contains(VisitId id) => _store.ContainsKey(id);

    // Kopia zapisanego stanu – do sekcji „then”.
    public Visit Stored(VisitId id) =>
        _store.TryGetValue(id, out var stored)
            ? Copy(stored)
            : throw new KeyNotFoundException($"Brak wizyty {id.Value} w repozytorium.");

    private static Visit Copy(Visit visit) =>
        Visit.Restore(
            visit.Id,
            visit.WorkOrderId,
            visit.TechnicianIds.ToArray(),
            visit.Status,
            visit.PlannedStart,
            visit.PlannedEnd,
            visit.StartedAt,
            visit.CompletedAt,
            visit.Scope,
            visit.ScopeVersion,
            visit.Signature,
            visit.Outcome,
            visit.CancelReason,
            visit.Version);
}
