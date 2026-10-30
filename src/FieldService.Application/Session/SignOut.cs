using FieldService.Application.Abstractions;

namespace FieldService.Application.Session;

// Wylogowanie: sesja (token, refresh token) i cache technika znikają; dane wizyt i niewysłane zmiany zostają –
// ten sam technik po zalogowaniu je wyśle, inny nie zaloguje się, dopóki czekają (SignInHandler).
public sealed class SignOutHandler(AccessTokenStore tokens, ISessionStore session, ILocalCaches caches)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        tokens.AccessToken = null;
        await session.ClearAsync();
        await caches.ClearAsync(ct);
    }
}
