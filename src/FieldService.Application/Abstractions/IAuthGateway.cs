using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Para tokenów sesji: access token (krótki, do nagłówka) i refresh token (jednorazowy, do odświeżenia)
public sealed record SessionTokens(string AccessToken, string RefreshToken, string TechnicianName);

// Port: logowanie technika i odświeżenie sesji
public interface IAuthGateway
{
    Task<Result<SessionTokens>> SignInAsync(string email, string pin, CancellationToken ct);

    // refresh token nieważny albo zużyty → Unauthorized
    Task<Result<SessionTokens>> RefreshAsync(string refreshToken, CancellationToken ct);
}
