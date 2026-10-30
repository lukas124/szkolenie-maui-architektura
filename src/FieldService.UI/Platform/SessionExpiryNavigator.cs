using FieldService.Application.Abstractions;
using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.UI.Extensions;

namespace FieldService.UI.Platform;

// Adapter portu ISessionExpiry: sesja wygasła (odświeżenie nieudane) → ekran logowania i jeden komunikat, na wątku UI.
// Dane w telefonie zostają – ten sam technik po zalogowaniu wysyła niewysłane zmiany.
public sealed class SessionExpiryNavigator(IDialogService dialogs, ILocalizer localizer, INavigationService navigation) : ISessionExpiry
{
    public void OnExpired() => MainThread.BeginInvokeOnMainThread(() => ShowLoginAsync().FireAndForget());

    private async Task ShowLoginAsync()
    {
        await navigation.ResetToAsync(new LoginRoute()); // zamyka też otwarte modale (np. kreator)
        await dialogs.ShowAlertAsync(localizer["Error_Title"], localizer["Error_Session.Expired"], localizer["Common_Ok"]);
    }
}
