using FieldService.Presentation.Common;
using FieldService.UI.Dialogs;

namespace FieldService.UI.Platform;

// Adapter portu IDialogService: dialog jako własna strona modalna (DialogPage) – ten sam wygląd na Androidzie i iOS.
// Port z Bloku 02 zostaje, zmienił się tylko adapter (wcześniej systemowe DisplayAlertAsync).
public sealed class MauiDialogService : IDialogService
{
    // true = dialog jest na ekranie; kolejny (np. drugi błąd w tej samej chwili) nie jest pokazywany
    private bool _isOpen;

    public Task ShowAlertAsync(string title, string message, string cancel) => ShowAsync(title, message, cancel, null);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        ShowAsync(title, message, accept, cancel);

    private Task<bool> ShowAsync(string title, string message, string accept, string? cancel) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_isOpen) return false;
            _isOpen = true;
            try
            {
                var dialog = new DialogPage(title, message, accept, cancel);
                await CurrentNavigation.PushModalAsync(dialog, false);
                return await dialog.Result;
            }
            finally
            {
                _isOpen = false;
            }
        });

    private static INavigation CurrentNavigation =>
        Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
        ?? throw new InvalidOperationException("Okno nie ma strony, na której można pokazać dialog.");
}
