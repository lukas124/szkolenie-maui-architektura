using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Session;

// Komenda: logowanie technika (dowolny e-mail + PIN)
public sealed record SignIn(string Email, string Pin);

public sealed class SignInHandler(IAuthGateway auth)
{
    public Task<Result<string>> HandleAsync(SignIn command, CancellationToken ct) =>
        auth.SignInAsync(command.Email.Trim(), command.Pin, ct);
}
