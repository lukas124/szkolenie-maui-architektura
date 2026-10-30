using FieldService.Application.Abstractions;

namespace FieldService.UI.Platform;

// Adapter portu ISessionStore: refresh token w SecureStorage (Keychain / Keystore) – nigdy w Preferences
public sealed class SecureSessionStore : ISessionStore
{
    private const string Key = "refresh_token";

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(Key);
        }
        catch (Exception)
        {
            // magazyn nieczytelny (np. po przywróceniu kopii na innym urządzeniu) – jak brak sesji
            SecureStorage.Default.Remove(Key);
            return null;
        }
    }

    public Task SaveRefreshTokenAsync(string refreshToken) => SecureStorage.Default.SetAsync(Key, refreshToken);

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
