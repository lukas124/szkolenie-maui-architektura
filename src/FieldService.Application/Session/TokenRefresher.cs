using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Session;

// Odświeżenie sesji po 401: jedno naraz – kilka żądań z wygasłym tokenem czeka na ten sam wynik.
// Refresh token jest jednorazowy (rotacja), więc drugie równoległe odświeżenie tym samym tokenem by się nie udało.
// Wynik: sukces = jest nowy access token; Unauthorized = sesja wygasła; inny błąd (Network) = sesja żyje, brak sieci.
public sealed class TokenRefresher(IAuthGateway auth, ISessionStore session, AccessTokenStore tokens, ISessionExpiry expiry)
{
    private static readonly Error NoSession = new("Session.Expired", ErrorType.Unauthorized);

    // jedna bramka na proces: TokenRefresher jest Transient (brama z HttpClient nie może żyć w singletonie),
    // a odświeżanie ma być jedno naraz dla całej aplikacji (także przywracanie sesji przy starcie)
    internal static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<Result> RefreshAsync(CancellationToken ct)
    {
        var expired = tokens.AccessToken;
        await Gate.WaitAsync(ct);
        try
        {
            // ktoś odświeżył, gdy czekaliśmy – nowy token już jest
            if (tokens.AccessToken is not null && tokens.AccessToken != expired) return Result.Success();

            var refreshToken = await session.GetRefreshTokenAsync();
            if (refreshToken is null) return Result.Failure(NoSession);

            var result = await auth.RefreshAsync(refreshToken, ct);
            if (result.IsSuccess)
            {
                await session.SaveRefreshTokenAsync(result.Value.RefreshToken);
                tokens.AccessToken = result.Value.AccessToken;
                return Result.Success();
            }

            // bez sieci sesja nie jest stracona – spróbujemy przy następnym żądaniu
            if (result.Error!.Type != ErrorType.Unauthorized) return Result.Failure(result.Error);

            tokens.AccessToken = null;
            await session.ClearAsync();
            expiry.OnExpired();
            return Result.Failure(result.Error);
        }
        finally
        {
            Gate.Release();
        }
    }
}
