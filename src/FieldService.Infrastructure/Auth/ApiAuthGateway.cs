using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Api;

namespace FieldService.Infrastructure.Auth;

// Adapter portu IAuthGateway: logowanie i odświeżenie sesji. Klient bez AuthHeaderHandler – odświeżenie tokenu nie
// może samo wywołać odświeżenia.
internal sealed class ApiAuthGateway(FieldServiceApiClient client, ApiExecutor executor) : IAuthGateway
{
    private static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", ErrorType.Unauthorized);

    public async Task<Result<SessionTokens>> SignInAsync(string email, string pin, CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("auth.login", c => client.LoginAsync(new LoginRequest { Email = email, Pin = pin }, c), ct);
        if (!result.IsSuccess) return result.Error!.Type == ErrorType.Unauthorized ? InvalidCredentials : result.Error!;

        return Result<SessionTokens>.Success(ToTokens(result.Value));
    }

    public async Task<Result<SessionTokens>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("auth.refresh", c => client.RefreshTokenAsync(new RefreshRequest { RefreshToken = refreshToken }, c), ct);
        return result.IsSuccess ? Result<SessionTokens>.Success(ToTokens(result.Value)) : result.Error!;
    }

    private static SessionTokens ToTokens(TokenResponse r) => new(r.AccessToken, r.RefreshToken, r.TechnicianName);
}
