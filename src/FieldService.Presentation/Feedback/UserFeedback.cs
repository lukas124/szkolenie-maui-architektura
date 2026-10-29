using FieldService.Domain.Common;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Feedback;

// Recepcja: błąd → tekst (IErrorMessageProvider) → tytuł i przyciski (ILocalizer) → okno (IDialogService)
public sealed class UserFeedback(IDialogService dialogs, IErrorMessageProvider messages, ILocalizer localizer) : IUserFeedback
{
    public Task ShowErrorAsync(Error error) =>
        dialogs.ShowAlertAsync(localizer["Error_Title"], messages.GetMessage(error), localizer["Common_Ok"]);

    public Task<bool> ConfirmAsync(string titleKey, string messageKey) =>
        dialogs.ConfirmAsync(localizer[titleKey], localizer[messageKey], localizer["Common_Yes"], localizer["Common_No"]);
}
