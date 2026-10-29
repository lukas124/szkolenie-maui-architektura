using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Protocols;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Zapytanie: protokół z bieżącego zakresu wizyty (to, co klient widzi przed zakończeniem)
public sealed record GetVisitProtocol(VisitId VisitId);

// Nazwy i ceny części z katalogu; bez sieci protokół i tak powstaje (nazwa = kod części, cena 0 – transformata
// VisitProtocolBuilder tak traktuje część spoza katalogu). Czas pracy liczony od rozpoczęcia wizyty.
public sealed class GetVisitProtocolHandler(IVisitRepository visits, IInventoryGateway inventory, IClock clock)
{
    public async Task<Result<VisitProtocol>> HandleAsync(GetVisitProtocol query, CancellationToken ct)
    {
        var visit = await visits.GetAsync(query.VisitId, ct);
        if (visit is null) return VisitErrors.NotFound;

        var catalog = await inventory.GetPartCatalogAsync(ct);
        IReadOnlyDictionary<PartId, PartInfo> parts = catalog.IsSuccess
            ? catalog.Value.ToDictionary(p => new PartId(p.PartId), p => new PartInfo(p.Name, p.UnitPrice))
            : new Dictionary<PartId, PartInfo>();

        var laborTime = visit.StartedAt is { } started ? clock.Now - started : TimeSpan.Zero;
        return Result<VisitProtocol>.Success(VisitProtocolBuilder.Build(visit.Scope, parts, laborTime));
    }
}
