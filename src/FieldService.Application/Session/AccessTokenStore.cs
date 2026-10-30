namespace FieldService.Application.Session;

// Access token zalogowanego technika – stan sesji w pamięci; zapisują go i czytają adaptery HTTP
// (refresh token w ISessionStore, odświeżenie: TokenRefresher)
public sealed class AccessTokenStore
{
    public string? AccessToken { get; set; }
}
