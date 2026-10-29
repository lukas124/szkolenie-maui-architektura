using FieldService.Domain.Common;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Feedback;

// Tekst błędu z zasobów: najpierw Error_{Code} (np. Error_Auth.InvalidCredentials), potem Error_{Type}
// (np. Error_Network), na końcu Error_Unexpected. Nowy błąd = nowy wpis w Strings.resx, bez zmian w kodzie.
public sealed class ErrorMessageProvider(ILocalizer localizer) : IErrorMessageProvider
{
    public string GetMessage(Error error) =>
        Find($"Error_{error.Code}") ?? Find($"Error_{error.Type}") ?? localizer["Error_Unexpected"];

    private string? Find(string key)
    {
        var text = localizer[key];
        return text == key ? null : text;
    }
}
