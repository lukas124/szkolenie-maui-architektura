using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Port: logowanie technika; sukces = imię i nazwisko technika
public interface IAuthGateway
{
    Task<Result<string>> SignInAsync(string email, string pin, CancellationToken ct);
}
