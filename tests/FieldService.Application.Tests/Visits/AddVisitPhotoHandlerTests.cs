using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Commands;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// Zdjęcie wizyty: uprawnienie → zdjęcie → zapis w IVisitPhotoStore z czasem z IClock (poza agregatem).
[TestFixture]
public class AddVisitPhotoHandlerTests
{
    private const string PhotoPath = "/data/zdjecia/wizyta-1.jpg";

    private Mock<IPermissionService> _permissions = null!;
    private Mock<IPhotoCapture> _camera = null!;
    private FakeVisitPhotoStore _photos = null!;
    private FixedClock _clock = null!;
    private AddVisitPhotoHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _permissions = new Mock<IPermissionService>();
        _camera = new Mock<IPhotoCapture>();
        _photos = new FakeVisitPhotoStore();
        _clock = new FixedClock();
        _handler = new AddVisitPhotoHandler(_permissions.Object, _camera.Object, _photos, _clock);
        _permissions.Setup(p => p.EnsureCameraAsync()).ReturnsAsync(Result.Success());
        _camera.Setup(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(PhotoPath));
    }

    [Test]
    public async Task PermissionAndPhoto_StoresPhotoWithClockTime()
    {
        var id = new VisitId(Guid.NewGuid());

        var result = await _handler.HandleAsync(new AddVisitPhoto(id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_photos.Photos, Is.EqualTo(new[] { new StoredPhoto(id, PhotoPath, _clock.Now) }));
        });
    }

    [Test]
    public async Task PermissionDenied_ReturnsDeniedWithoutCapturing()
    {
        _permissions.Setup(p => p.EnsureCameraAsync()).ReturnsAsync(Result.Failure(DeviceErrors.PermissionDenied));

        var result = await _handler.HandleAsync(new AddVisitPhoto(new VisitId(Guid.NewGuid())), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(DeviceErrors.PermissionDenied));
            Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Validation));
            _camera.Verify(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.That(_photos.Photos, Is.Empty);
        });
    }

    [Test]
    public async Task PermissionDeniedPermanently_ReturnsDeniedPermanentlyWithoutCapturing()
    {
        _permissions.Setup(p => p.EnsureCameraAsync())
            .ReturnsAsync(Result.Failure(DeviceErrors.PermissionDeniedPermanently));

        var result = await _handler.HandleAsync(new AddVisitPhoto(new VisitId(Guid.NewGuid())), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(DeviceErrors.PermissionDeniedPermanently));
            _camera.Verify(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.That(_photos.Photos, Is.Empty);
        });
    }

    [Test]
    public async Task PhotoCancelled_ReturnsCancelledAndStoresNothing()
    {
        _camera.Setup(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(DeviceErrors.PhotoCancelled));

        var result = await _handler.HandleAsync(new AddVisitPhoto(new VisitId(Guid.NewGuid())), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(DeviceErrors.PhotoCancelled));
            Assert.That(_photos.Photos, Is.Empty);
        });
    }

    [Test]
    public async Task CameraFailure_ReturnsCameraErrorAndStoresNothing()
    {
        var failure = new Error("Photo.Failed", ErrorType.Unexpected);
        _camera.Setup(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(failure));

        var result = await _handler.HandleAsync(new AddVisitPhoto(new VisitId(Guid.NewGuid())), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(failure));
            Assert.That(_photos.Photos, Is.Empty);
        });
    }
}
