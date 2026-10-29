using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Session;

// Komenda: logowanie technika (dowolny e-mail + PIN)
public sealed record SignIn(string Email, string Pin);

public static class SessionErrors
{
    // poprzedni technik ma w telefonie zmiany, które nie dotarły na serwer – najpierw musi je wysłać on sam
    public static readonly Error UnsentChangesOfOtherTechnician = new("Session.UnsentChangesOfOtherTechnician", ErrorType.Conflict);
}

// Logowanie: dane w telefonie należą do jednego technika. Inny technik → dane poprzedniego usuwamy (po udanym
// logowaniu), chyba że czekają w nich niewysłane zmiany – wtedy logowanie odmawia, zanim cokolwiek zginie.
public sealed class SignInHandler(IAuthGateway auth, IPreferencesStore preferences, ILocalTechnicianData localData)
{
    private const string OwnerKey = "local_data_owner";

    public async Task<Result<string>> HandleAsync(SignIn command, CancellationToken ct)
    {
        var email = command.Email.Trim();
        var owner = preferences.Get(OwnerKey);
        var otherTechnician = owner is not null && !string.Equals(owner, email, StringComparison.OrdinalIgnoreCase);
        if (otherTechnician && await localData.HasUnsentChangesAsync(ct))
            return Result<string>.Failure(SessionErrors.UnsentChangesOfOtherTechnician);

        var result = await auth.SignInAsync(email, command.Pin, ct);
        if (!result.IsSuccess) return result;

        if (otherTechnician) await localData.ClearAsync(ct);
        preferences.Set(OwnerKey, email);
        return result;
    }
}
