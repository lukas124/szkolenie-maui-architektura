using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FieldService.Application.Session;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;

namespace FieldService.Application.Visits;

// TODO: Ćwiczenie 02 – brama HTTP to adapter: przenieś ten plik (oraz VisitDto.cs i VisitDtoMapper.cs)
// do src/FieldService.Infrastructure/Visits i niech implementuje port IVisitsGateway
// GET api/visits/today → VisitSummary; wyjątki sieci → Result z błędem
public sealed class HttpVisitsGateway(HttpClient http, AccessTokenStore tokens)
{
    public async Task<Result<IReadOnlyList<VisitSummary>>> GetTodayAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/visits/today");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return Result<IReadOnlyList<VisitSummary>>.Failure(new Error("Visits.Unauthorized", ErrorType.Unauthorized));
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<VisitSummary>>.Failure(new Error("Visits.Unexpected", ErrorType.Unexpected));

            var dtos = await response.Content.ReadFromJsonAsync<List<VisitDto>>(ct) ?? [];
            IReadOnlyList<VisitSummary> visits = dtos.Select(VisitDtoMapper.ToSummary).ToList();
            return Result<IReadOnlyList<VisitSummary>>.Success(visits);
        }
        catch (Exception ex) when (IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return Result<IReadOnlyList<VisitSummary>>.Failure(new Error("Visits.Network", ErrorType.Network));
        }
        catch (JsonException)
        {
            // odpowiedź to nie JSON (np. Wi-Fi z portalem logowania zwraca stronę HTML)
            return Result<IReadOnlyList<VisitSummary>>.Failure(new Error("Visits.Unexpected", ErrorType.Unexpected));
        }
    }

    // Android zgłasza brak sieci także jako WebException albo IOException
    private static bool IsNetwork(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or WebException or IOException;
}
