using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Przewidywalne wyniki pracy z urządzeniem – nie wyjątki
public static class DeviceErrors
{
    // Odmowa uprawnienia to decyzja użytkownika, nie wygasła sesja – dlatego Validation, nie Unauthorized
    public static readonly Error PermissionDenied = new("Permission.Denied", ErrorType.Validation);
    public static readonly Error PermissionDeniedPermanently = new("Permission.DeniedPermanently", ErrorType.Validation);
    public static readonly Error PhotoCancelled = new("Photo.Cancelled", ErrorType.Validation);
    public static readonly Error CameraUnavailable = new("Photo.CameraUnavailable", ErrorType.Validation);
}
