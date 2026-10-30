namespace FieldService.Application.Abstractions;

// Port: refresh token w bezpiecznym magazynie telefonu (UI: SecureStorage) – nigdy w Preferences
public interface ISessionStore
{
    Task<string?> GetRefreshTokenAsync();

    Task SaveRefreshTokenAsync(string refreshToken);

    Task ClearAsync();
}
