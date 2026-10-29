using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using Microsoft.Maui.Graphics.Platform;

namespace FieldService.UI.Platform;

// Adapter portu IPhotoCapture: zdjęcie z aparatu, zmniejszone (dłuższy bok maks. 1600 px, JPEG 85%) i zapisane
// w katalogu aplikacji – oryginał z aparatu (kilka MB) nie zostaje w pamięci ani w bazie
public sealed class MauiPhotoCapture(IAppPaths paths) : IPhotoCapture
{
    private const float MaxSide = 1600;
    private const float JpegQuality = 0.85f;

    public async Task<Result<string>> CapturePhotoAsync(CancellationToken ct)
    {
        if (!MediaPicker.Default.IsCaptureSupported) return DeviceErrors.CameraUnavailable;

        var file = await MainThread.InvokeOnMainThreadAsync(() => MediaPicker.Default.CapturePhotoAsync());
        if (file is null) return DeviceErrors.PhotoCancelled;

        var directory = Path.Combine(paths.DataDirectory, "photos");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.jpg");

        await using var source = await file.OpenReadAsync();
        var image = PlatformImage.FromStream(source);
        if (Math.Max(image.Width, image.Height) > MaxSide)
            image = image.Downsize(MaxSide, disposeOriginal: true);
        await using (var output = File.Create(path))
            await image.SaveAsync(output, ImageFormat.Jpeg, JpegQuality);
        image.Dispose();

        return Result<string>.Success(path);
    }
}
