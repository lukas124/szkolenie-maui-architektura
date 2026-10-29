using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Zapytanie: szczegóły wizyty z lokalnej repliki. Stanu wizyty nie zmienia – co najwyżej odświeża lokalną kopię danych
// z serwera: przy otwarciu ekranu (Refresh) albo gdy wizyty jeszcze nie ma w telefonie. Bez sieci – to, co w replice.
public sealed record GetVisitDetails(VisitId VisitId, bool Refresh = false);

public sealed class GetVisitDetailsHandler(IVisitReplica replica, IVisitsGateway gateway, IVisitPhotoStore photos)
{
    public async Task<Result<VisitDetails>> HandleAsync(GetVisitDetails query, CancellationToken ct)
    {
        var details = query.Refresh ? null : await replica.GetDetailsAsync(query.VisitId, ct);
        if (details is null)
        {
            var remote = await gateway.GetVisitAsync(query.VisitId, ct);
            if (remote.IsSuccess) await replica.SeedAsync(remote.Value, ct);

            details = await replica.GetDetailsAsync(query.VisitId, ct);
            if (details is null) return remote.IsSuccess ? VisitErrors.NotFound : remote.Error!;
        }

        return Result<VisitDetails>.Success(details with { PhotoCount = await photos.CountAsync(query.VisitId, ct) });
    }
}
