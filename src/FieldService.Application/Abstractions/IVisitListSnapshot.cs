using FieldService.Application.Visits.Queries;

namespace FieldService.Application.Abstractions;

// Ostatnia lista wizyt pobrana z serwera, z chwilą pobrania – bez sieci lista pokazuje ją zamiast pustego ekranu
public sealed record VisitListSnapshot(IReadOnlyList<VisitSummary> Visits, DateTimeOffset SavedAt);

// Port: migawka listy wizyt w telefonie (jedna, nadpisywana po każdym udanym pobraniu)
public interface IVisitListSnapshotStore
{
    Task SaveAsync(VisitListSnapshot snapshot, CancellationToken ct);

    // null = lista jeszcze nigdy nie została pobrana
    Task<VisitListSnapshot?> GetAsync(CancellationToken ct);
}
