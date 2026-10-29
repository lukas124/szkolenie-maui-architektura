namespace FieldService.Presentation.Common;

// Port prezentacji: okno z komunikatem albo pytaniem (implementacja MAUI w UI)
public interface IDialogService
{
    Task ShowAlertAsync(string title, string message, string cancel);

    // true = użytkownik wybrał „accept”
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}
