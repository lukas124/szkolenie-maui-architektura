using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;

namespace FieldService.Application.Abstractions;

// Port: lokalna replika wizyt w telefonie – odczyt szczegółów (model odczytu) i zasilenie danymi z serwera
public interface IVisitReplica
{
    // nieznane Id → null
    Task<VisitDetails?> GetDetailsAsync(VisitId id, CancellationToken ct);

    // wizyta bez lokalnych zmian dostaje świeży stan z serwera; wizyta ze zmianami technika zostaje bez zmian
    Task SeedAsync(RemoteVisit visit, CancellationToken ct);

    // wizyty z niewysłanymi zmianami technika: Id → status lokalny
    Task<IReadOnlyDictionary<Guid, VisitStatus>> GetLocallyChangedStatusesAsync(CancellationToken ct);
}
