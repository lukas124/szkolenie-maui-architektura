using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Port: uprawnienia w czasie działania (deklaracje: AndroidManifest.xml, Info.plist). Implementacja MAUI w UI.
public interface IPermissionService
{
    // Sprawdza i w razie potrzeby prosi o dostęp do aparatu (DeviceErrors.PermissionDenied / PermissionDeniedPermanently)
    Task<Result> EnsureCameraAsync();

    // true = system pokaże jeszcze okno zgody, ale to ostatnia szansa – najpierw nasze wyjaśnienie
    bool ShouldExplainCamera();

    // Ustawienia aplikacji w systemie (po odmowie na stałe tylko tam można nadać uprawnienie)
    void OpenAppSettings();
}
