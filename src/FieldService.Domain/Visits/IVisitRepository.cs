using FieldService.Domain.Common;

namespace FieldService.Domain.Visits;

// Repozytorium agregatu – część modelu (kolekcja wizyt); implementacja (SQLite) w Infrastructure
public interface IVisitRepository
{
    // nieznane Id → null
    Task<Visit?> GetAsync(VisitId id, CancellationToken ct);

    Task AddAsync(Visit visit, CancellationToken ct);

    // Zapis całości albo nic. Ktoś zapisał wcześniej (inna wersja) → VisitErrors.VersionConflict.
    // Sukces → wersja + 1 i wyczyszczone zdarzenia.
    Task<Result> SaveAsync(Visit visit, CancellationToken ct);

    // Czy w telefonie jest w toku inna wizyta niż podana (dane w telefonie należą do jednego technika)
    Task<bool> HasOtherInProgressAsync(VisitId visitId, CancellationToken ct);
}
