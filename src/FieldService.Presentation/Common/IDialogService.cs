namespace FieldService.Presentation.Common;

// Port prezentacji: okno z komunikatem (implementacja MAUI w UI)
public interface IDialogService
{
    Task ShowAlertAsync(string title, string message, string cancel);
}
