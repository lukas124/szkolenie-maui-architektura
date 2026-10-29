using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Commands;

public sealed record AddVisitPhoto(VisitId VisitId);

// Zdjęcie wizyty: uprawnienie (w momencie użycia funkcji) → aparat → zapis poza agregatem
public sealed class AddVisitPhotoHandler(IPermissionService permissions, IPhotoCapture camera, IVisitPhotoStore photos, IClock clock)
{
    public async Task<Result> HandleAsync(AddVisitPhoto command, CancellationToken ct)
    {
        var allowed = await permissions.EnsureCameraAsync();
        if (!allowed.IsSuccess) return allowed;

        var photo = await camera.CapturePhotoAsync(ct);
        if (!photo.IsSuccess) return photo.Error!;

        await photos.AddAsync(command.VisitId, photo.Value, clock.Now, ct);
        return Result.Success();
    }
}
