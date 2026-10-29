using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Http;
using FieldService.Infrastructure.Outbox;

namespace FieldService.Infrastructure.Visits;

// Adapter portu IVisitsGateway: API wizyt → modele aplikacji; wyjątki sieci → Result z błędem.
// Wysyła też zmiany z outbox: wiadomość → żądanie z kluczem idempotencji i wersją, którą widział telefon.
public sealed class HttpVisitsGateway(HttpClient http, AccessTokenStore tokens) : IVisitsGateway, IOutboxSender
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

    public async Task<Result<VisitHistoryPageResult>> GetHistoryAsync(int page, int pageSize, CancellationToken ct)
    {
        try
        {
            using var request = AuthorizedRequest.Create(HttpMethod.Get, $"api/visits/history?page={page}&pageSize={pageSize}", tokens);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return ToError(response.StatusCode);

            var dto = await response.Content.ReadFromJsonAsync<VisitHistoryPageDto>(ct);
            return Result<VisitHistoryPageResult>.Success(VisitDtoMapper.ToHistoryPage(dto!));
        }
        catch (Exception ex) when (NetworkFailures.IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return new Error("Visits.Network", ErrorType.Network);
        }
        catch (JsonException)
        {
            return new Error("Visits.Unexpected", ErrorType.Unexpected);
        }
    }

    public async Task<Result<int>> SendAsync(OutboxMessage message, int expectedVersion, CancellationToken ct)
    {
        try
        {
            using var request = ToRequest(message);
            if (request is null) return new Error("Visits.Unexpected", ErrorType.Unexpected);
            request.Headers.Add("Idempotency-Key", message.Id);
            // proxy hostingu wycina If-Match – serwer czyta tę samą wartość z X-If-Match
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{expectedVersion}\"");
            request.Headers.Add("X-If-Match", $"\"{expectedVersion}\"");
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(ct);
                return problem?.Code == VisitErrors.VersionConflict.Code
                    ? VisitErrors.VersionConflict
                    : new Error("Visits.Unexpected", ErrorType.Unexpected);
            }
            // limit żądań serwera – chwilowy, jak brak sieci: kolejka czeka, próba się nie liczy
            if (response.StatusCode == HttpStatusCode.TooManyRequests) return new Error("Visits.Network", ErrorType.Network);
            if (!response.IsSuccessStatusCode) return ToError(response.StatusCode);

            var dto = await response.Content.ReadFromJsonAsync<VisitDetailsDto>(ct);
            return Result<int>.Success(dto!.Version);
        }
        catch (Exception ex) when (NetworkFailures.IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return new Error("Visits.Network", ErrorType.Network);
        }
        catch (JsonException)
        {
            return new Error("Visits.Unexpected", ErrorType.Unexpected);
        }
    }

    // Mapa wiadomość → żądanie: typ zdarzenia wybiera metodę i adres, Payload to gotowe ciało
    private HttpRequestMessage? ToRequest(OutboxMessage message)
    {
        var visit = $"api/visits/{message.VisitId}";
        (HttpMethod Method, string Path)? target = message.Type switch
        {
            "VisitStarted" => (HttpMethod.Post, $"{visit}/start"),
            "ActivityRecorded" => (HttpMethod.Post, $"{visit}/activities"),
            "PartUsed" => (HttpMethod.Post, $"{visit}/parts"),
            "PartRemoved" => (HttpMethod.Delete, $"{visit}/parts/{Uri.EscapeDataString(PartIdOf(message))}"),
            "CustomerSigned" => (HttpMethod.Put, $"{visit}/signature"),
            "VisitCompleted" => (HttpMethod.Post, $"{visit}/complete"),
            _ => null
        };
        if (target is not var (method, path)) return null;

        var request = AuthorizedRequest.Create(method, path, tokens);
        if (method != HttpMethod.Delete)
            request.Content = new StringContent(message.Payload, Encoding.UTF8, "application/json");
        return request;
    }

    private static string PartIdOf(OutboxMessage message)
    {
        using var payload = JsonDocument.Parse(message.Payload);
        return payload.RootElement.GetProperty("partId").GetString() ?? "";
    }

    // problem+json z API: stabilny kod odmowy
    private sealed class ProblemDto
    {
        public string? Code { get; set; }
    }

    private static Error ToError(HttpStatusCode status) => status == HttpStatusCode.Unauthorized
        ? new Error("Visits.Unauthorized", ErrorType.Unauthorized)
        : new Error("Visits.Unexpected", ErrorType.Unexpected);
}
