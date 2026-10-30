using System.Net;
using System.Net.Http.Headers;
using FieldService.Application.Session;
using FieldService.Domain.Common;

namespace FieldService.Infrastructure.Http;

// Dekorator potoku HttpClient: token w nagłówku. Odpowiedź 401 → jedno odświeżenie sesji (TokenRefresher) i jedno
// ponowienie żądania z nowym tokenem. Sesja wygasła → 401 idzie dalej (ApiExecutor: Unauthorized). Odświeżenie
// nieudane bez sieci (sesja żyje) → HttpRequestException (ApiExecutor: Network), a nie „zaloguj się ponownie”.
public sealed class AuthHeaderHandler(AccessTokenStore tokens, TokenRefresher refresher) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Authorize(request);
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
        var refreshed = await refresher.RefreshAsync(ct);
        if (!refreshed.IsSuccess)
        {
            if (refreshed.Error!.Type == ErrorType.Unauthorized) return response;
            response.Dispose();
            throw new HttpRequestException("Nie udało się odświeżyć sesji – brak połączenia.");
        }

        response.Dispose();
        Authorize(request);
        return await base.SendAsync(request, ct);
    }

    private void Authorize(HttpRequestMessage request) =>
        request.Headers.Authorization = tokens.AccessToken is { } token ? new AuthenticationHeaderValue("Bearer", token) : null;
}
