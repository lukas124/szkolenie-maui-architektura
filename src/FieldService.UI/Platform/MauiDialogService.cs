using FieldService.Presentation.Common;

namespace FieldService.UI.Platform;

// Adapter portu IDialogService: systemowe okno MAUI
public sealed class MauiDialogService : IDialogService
{
    public Task ShowAlertAsync(string title, string message, string cancel) =>
        Microsoft.Maui.Controls.Application.Current!.Windows[0].Page!.DisplayAlertAsync(title, message, cancel);
}
