namespace FieldService.Application.Session;

// Access token zalogowanego technika – stan sesji w pamięci; zapisują go i czytają adaptery HTTP
// (odświeżanie tokenu i bezpieczny zapis sesji: Blok 13)
public sealed class AccessTokenStore
{
    public string? AccessToken { get; set; }
}
