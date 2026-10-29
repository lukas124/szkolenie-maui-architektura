using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Port: zdjęcie z aparatu zapisane w katalogu aplikacji; wynik = ścieżka pliku (anulowanie → DeviceErrors.PhotoCancelled)
public interface IPhotoCapture
{
    Task<Result<string>> CapturePhotoAsync(CancellationToken ct);
}
