using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.UI.Platform;

// Adapter portu IPermissionService (Permissions z MAUI). Odmowa na stałe: Android – po drugiej odmowie system nie
// pokazuje już okna (ShouldShowRationale = false), iOS – pyta tylko raz, potem wyłącznie Ustawienia.
public sealed class MauiPermissionService : IPermissionService
{
    public Task<Result> EnsureCameraAsync() =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status == PermissionStatus.Granted) return Result.Success();

            if (DeviceInfo.Platform == DevicePlatform.iOS && status == PermissionStatus.Denied)
                return Result.Failure(DeviceErrors.PermissionDeniedPermanently); // iOS nie zapyta drugi raz

            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status == PermissionStatus.Granted) return Result.Success();

            if (DeviceInfo.Platform == DevicePlatform.iOS)
                return Result.Failure(DeviceErrors.PermissionDeniedPermanently); // jedyna prośba iOS właśnie przepadła

            // Android: brak uzasadnienia po odmowie = „nie pytaj ponownie” (system już nie pokaże okna)
            if (DeviceInfo.Platform == DevicePlatform.Android && !Permissions.ShouldShowRationale<Permissions.Camera>())
                return Result.Failure(DeviceErrors.PermissionDeniedPermanently);

            return Result.Failure(DeviceErrors.PermissionDenied);
        });

    public bool ShouldExplainCamera() =>
        DeviceInfo.Platform == DevicePlatform.Android && Permissions.ShouldShowRationale<Permissions.Camera>();

    public void OpenAppSettings() => AppInfo.Current.ShowSettingsUI();
}
