using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Session;

// Start aplikacji: zapamiętana sesja → od razu praca (bez ekranu logowania). Cichy – bez komunikatu o wygaśnięciu.
// Bez sieci sesja też zostaje (lista z migawki, praca offline): token odświeży się przy pierwszym 401, gdy wróci sieć.
public sealed class RestoreSessionHandler(IAuthGateway auth, ISessionStore session, AccessTokenStore tokens)
{
    // true = sesja przywrócona albo zachowana bez sieci
    public async Task<bool> HandleAsync(CancellationToken ct)
    {
        await TokenRefresher.Gate.WaitAsync(ct); // to samo „jedno odświeżenie naraz” co po 401
        try
        {
            if (tokens.AccessToken is not null) return true; // sesja już jest (np. drugie okno na Androidzie)

            var refreshToken = await session.GetRefreshTokenAsync();
            if (refreshToken is null) return false;

            var result = await auth.RefreshAsync(refreshToken, ct);
            if (result.IsSuccess)
            {
                await session.SaveRefreshTokenAsync(result.Value.RefreshToken);
                tokens.AccessToken = result.Value.AccessToken;
                return true;
            }

            // serwer odrzucił refresh token – sesja wygasła; inny błąd (brak sieci) – sesja zostaje
            if (result.Error!.Type != ErrorType.Unauthorized) return true;
            await session.ClearAsync();
            return false;
        }
        finally
        {
            TokenRefresher.Gate.Release();
        }
    }
}
