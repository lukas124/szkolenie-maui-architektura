using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Http;

namespace FieldService.Infrastructure.Visits;

// Adapter portu IVisitsGateway: API wizyt → modele aplikacji; wyjątki sieci → Result z błędem
public sealed class HttpVisitsGateway(HttpClient http, AccessTokenStore tokens) : IVisitsGateway
{
    public async Task<Result<IReadOnlyList<VisitSummary>>> GetTodayAsync(CancellationToken ct)
    {
        try
        {
            using var request = AuthorizedRequest.Create(HttpMethod.Get, "api/visits/today", tokens);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return ToError(response.StatusCode);

            var dtos = await response.Content.ReadFromJsonAsync<List<VisitDto>>(ct) ?? [];
            IReadOnlyList<VisitSummary> visits = dtos.Select(VisitDtoMapper.ToSummary).ToList();
            return Result<IReadOnlyList<VisitSummary>>.Success(visits);
        }
        catch (Exception ex) when (NetworkFailures.IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return new Error("Visits.Network", ErrorType.Network);
        }
        catch (JsonException)
        {
            // odpowiedź to nie JSON (np. Wi-Fi z portalem logowania zwraca stronę HTML)
            return new Error("Visits.Unexpected", ErrorType.Unexpected);
        }
    }

    public async Task<Result<RemoteVisit>> GetVisitAsync(VisitId id, CancellationToken ct)
    {
        try
        {
            using var request = AuthorizedRequest.Create(HttpMethod.Get, $"api/visits/{id.Value}", tokens);
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return VisitErrors.NotFound;
            if (!response.IsSuccessStatusCode) return ToError(response.StatusCode);

            var dto = await response.Content.ReadFromJsonAsync<VisitDetailsDto>(ct);
            return Result<RemoteVisit>.Success(VisitDtoMapper.ToRemoteVisit(dto!));
        }
        catch (Exception ex) when (NetworkFailures.IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return new Error("Visits.Network", ErrorType.Network);
        }
        catch (JsonException)
        {
            // odpowiedź to nie JSON (np. Wi-Fi z portalem logowania zwraca stronę HTML)
            return new Error("Visits.Unexpected", ErrorType.Unexpected);
        }
    }

    private static Error ToError(HttpStatusCode status) => status == HttpStatusCode.Unauthorized
        ? new Error("Visits.Unauthorized", ErrorType.Unauthorized)
        : new Error("Visits.Unexpected", ErrorType.Unexpected);
}
