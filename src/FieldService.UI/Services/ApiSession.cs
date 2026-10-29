using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace FieldService.UI.Services;

// Sesja technika: logowanie (e-mail + PIN) i access token w pamięci.
// Odświeżanie tokenu i bezpieczny zapis sesji dojdą w Bloku 13.
public class ApiSession
{
    private readonly HttpClient _http;

    public ApiSession(IConfiguration configuration)
    {
        BaseUrl = configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Brak Api:BaseUrl w appsettings.json.");
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    public string BaseUrl { get; }
    public string? AccessToken { get; private set; }
    public string? TechnicianName { get; private set; }

    // null = zalogowano; tekst = komunikat dla technika
    public async Task<string?> SignInAsync(string email, string pin, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", new { email, pin }, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return "Nieprawidłowy e-mail lub PIN.";
            response.EnsureSuccessStatusCode();

            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            AccessToken = tokens!.AccessToken;
            TechnicianName = tokens.TechnicianName;
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or WebException or IOException)
        {
            return "Brak połączenia z serwerem. Sprawdź internet i spróbuj ponownie.";
        }
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TechnicianName);
}
