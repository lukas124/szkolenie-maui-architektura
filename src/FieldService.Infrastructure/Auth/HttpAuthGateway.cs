using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Http;

namespace FieldService.Infrastructure.Auth;

// Adapter portu IAuthGateway: POST api/auth/login, token trafia do AccessTokenStore
public sealed class HttpAuthGateway(HttpClient http, AccessTokenStore tokens) : IAuthGateway
{
    public async Task<Result<string>> SignInAsync(string email, string pin, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/auth/login", new { email, pin }, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return Result<string>.Failure(new Error("Auth.InvalidCredentials", ErrorType.Unauthorized));
            if (!response.IsSuccessStatusCode)
                return Result<string>.Failure(new Error("Auth.Unexpected", ErrorType.Unexpected));

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            if (body is null) return Result<string>.Failure(new Error("Auth.Unexpected", ErrorType.Unexpected));
            tokens.AccessToken = body.AccessToken;
            return Result<string>.Success(body.TechnicianName);
        }
        catch (Exception ex) when (NetworkFailures.IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return Result<string>.Failure(new Error("Auth.Network", ErrorType.Network));
        }
        catch (JsonException)
        {
            // odpowiedź to nie JSON (np. Wi-Fi z portalem logowania zwraca stronę HTML)
            return Result<string>.Failure(new Error("Auth.Unexpected", ErrorType.Unexpected));
        }
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TechnicianName);
}
