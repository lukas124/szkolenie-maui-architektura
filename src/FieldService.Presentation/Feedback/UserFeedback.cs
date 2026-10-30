using FieldService.Domain.Common;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Feedback;

// Recepcja: błąd → tekst (IErrorMessageProvider) → tytuł i przyciski (ILocalizer) → okno (IDialogService)
public sealed class UserFeedback(IDialogService dialogs, IErrorMessageProvider messages, ILocalizer localizer) : IUserFeedback
{
    // Unauthorized pomijamy: o wygasłej sesji mówi jeden komunikat na ekranie logowania (ISessionExpiry),
    // a nie każde żądanie, które w tej chwili dostało 401
    public Task ShowErrorAsync(Error error) =>
        error.Type == ErrorType.Unauthorized
            ? Task.CompletedTask
            : dialogs.ShowAlertAsync(localizer["Error_Title"], messages.GetMessage(error), localizer["Common_Ok"]);

    public Task<bool> ConfirmAsync(string titleKey, string messageKey) =>
        ConfirmAsync(titleKey, messageKey, "Common_Yes", "Common_No");

    public Task<bool> ConfirmAsync(string titleKey, string messageKey, string acceptKey, string cancelKey) =>
        dialogs.ConfirmAsync(localizer[titleKey], localizer[messageKey], localizer[acceptKey], localizer[cancelKey]);

    public Task<string?> ChooseAsync(string titleKey, IReadOnlyList<string> options) =>
        dialogs.ChooseAsync(localizer[titleKey], localizer["Common_Cancel"], options);
}
