using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Session;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Visits;
using FieldService.TestDoubles;

namespace FieldService.Presentation.Tests.Visits;

// ViewModel szczegółów wizyty: błąd przypadku użycia → komunikat; trzy reakcje na błąd zdjęcia.
// Handlery są prawdziwe (sealed), podmieniamy tylko porty.
[TestFixture]
public class VisitDetailsViewModelTests
{
    private const string CustomerName = "Jan Klient";

    private InMemoryVisitRepository _visits = null!;
    private FixedClock _clock = null!;
    private FakeVisitPhotoStore _photos = null!;
    private Mock<IVisitReplica> _replica = null!;
    private Mock<IVisitsGateway> _gateway = null!;
    private Mock<IPermissionService> _permissions = null!;
    private Mock<IPhotoCapture> _camera = null!;
    private Mock<IInventoryGateway> _inventory = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private VisitId _visitId;
    private VisitDetails _details = null!;
    private VisitDetailsViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _visits = new InMemoryVisitRepository();
        _clock = new FixedClock();
        _photos = new FakeVisitPhotoStore();
        _replica = new Mock<IVisitReplica>();
        _gateway = new Mock<IVisitsGateway>();
        _permissions = new Mock<IPermissionService>();
        _camera = new Mock<IPhotoCapture>();
        _inventory = new Mock<IInventoryGateway>();
        _feedback = new Mock<IUserFeedback>();

        _visitId = new VisitId(Guid.NewGuid());
        _details = Details(_visitId);
        _visits.Add(TestVisits.WithScope(_visitId));
        _replica.Setup(r => r.GetDetailsAsync(_visitId, It.IsAny<CancellationToken>())).ReturnsAsync(_details);
        // ekran przy otwarciu odświeża replikę z serwera – tu bez sieci, więc dane z repliki
        _gateway.Setup(g => g.GetVisitAsync(_visitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Error("Network.Unavailable", ErrorType.Network));
        _permissions.Setup(p => p.EnsureCameraAsync()).ReturnsAsync(Result.Success());
        _permissions.Setup(p => p.ShouldExplainCamera()).Returns(false);
        _camera.Setup(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("/data/zdjecia/1.jpg"));

        _viewModel = new VisitDetailsViewModel(
            new GetVisitDetailsHandler(_replica.Object, _gateway.Object, _photos),
            new StartVisitHandler(_visits, _clock),
            new UsePartHandler(_visits, _clock),
            new SignByCustomerHandler(_visits, _clock),
            new CompleteVisitHandler(_visits, _clock),
            new AddVisitPhotoHandler(_permissions.Object, _camera.Object, _photos, _clock),
            new GetVanStockHandler(_inventory.Object),
            _permissions.Object,
            new VisitFormatter(new TestLocalizer(), _clock),
            _feedback.Object);
    }

    [Test]
    public async Task NavigatedTo_LoadsDetails()
    {
        await _viewModel.OnNavigatedToAsync(_visitId);

        Assert.That(_viewModel.Details, Is.EqualTo(_details));
    }

    [Test]
    public async Task NavigatedTo_LoadError_ShowsError()
    {
        var offline = new Error("Network.Offline", ErrorType.Network);
        _replica.Setup(r => r.GetDetailsAsync(_visitId, It.IsAny<CancellationToken>())).ReturnsAsync((VisitDetails?)null);
        _gateway.Setup(g => g.GetVisitAsync(_visitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoteVisit>.Failure(offline));

        await _viewModel.OnNavigatedToAsync(_visitId);

        _feedback.Verify(f => f.ShowErrorAsync(offline), Times.Once);
    }

    // ---------- Zakończ ----------

    // Checkpoint 07: „Zakończ – naprawione” bez podpisu → komunikat SignatureRequired, bez zapisu.
    [Test]
    public async Task Complete_WithoutSignature_ShowsSignatureRequired()
    {
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.CompleteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _feedback.Verify(f => f.ShowErrorAsync(VisitErrors.SignatureRequired), Times.Once);
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Complete_WithSignature_SavesAndReloadsDetails()
    {
        _visits.Add(TestVisits.Signed(_visitId));
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.CompleteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
            Assert.That(_visits.Stored(_visitId).Status, Is.EqualTo(VisitStatus.Completed));
            _replica.Verify(r => r.GetDetailsAsync(_visitId, It.IsAny<CancellationToken>()), Times.AtLeast(2));
        });
    }

    // Konflikt (np. wizyta już rozpoczęta na drugim telefonie) → komunikat i świeże dane
    [Test]
    public async Task Start_Conflict_ShowsErrorAndReloads()
    {
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.StartCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _feedback.Verify(f => f.ShowErrorAsync(VisitErrors.InvalidTransition), Times.Once);
            _replica.Verify(r => r.GetDetailsAsync(_visitId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        });
    }

    // ---------- Podpis i część ----------

    [Test]
    public async Task Sign_Confirmed_SignsWithCustomerName()
    {
        _feedback.Setup(f => f.ConfirmAsync("Visit_SignTitle", "Visit_SignMessage")).ReturnsAsync(true);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.SignCommand.ExecuteAsync(null);

        Assert.That(_visits.Stored(_visitId).Signature?.SignerName, Is.EqualTo(CustomerName));
    }

    [Test]
    public async Task Sign_NotConfirmed_DoesNotSave()
    {
        _feedback.Setup(f => f.ConfirmAsync("Visit_SignTitle", "Visit_SignMessage")).ReturnsAsync(false);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.SignCommand.ExecuteAsync(null);

        Assert.That(_visits.SaveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task AddPart_ChoiceCancelled_DoesNotSave()
    {
        GivenVanStock();
        _feedback.Setup(f => f.ChooseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync((string?)null);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPartCommand.ExecuteAsync(null);

        Assert.That(_visits.SaveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task AddPart_PartChosen_UsesOnePieceOfThatPart()
    {
        GivenVanStock();
        _feedback.Setup(f => f.ChooseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync("Filtr siatkowy");
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPartCommand.ExecuteAsync(null);

        Assert.That(_visits.Stored(_visitId).Scope.Parts,
            Is.EqualTo(new[] { new UsedPart(TestVisits.Filter, Quantity.Create(1).Value) }));
    }

    // ---------- Zdjęcie: trzy reakcje na błąd ----------

    [Test]
    public async Task AddPhoto_Success_StoresPhotoWithoutMessages()
    {
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_photos.Photos, Has.Count.EqualTo(1));
            _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
        });
    }

    // Photo.Cancelled → nic: ani komunikatu, ani wyjaśnienia.
    [Test]
    public async Task AddPhoto_Cancelled_DoesNothing()
    {
        _camera.Setup(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(DeviceErrors.PhotoCancelled));
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
            _feedback.Verify(f => f.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _feedback.Verify(f => f.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _permissions.Verify(p => p.OpenAppSettings(), Times.Never);
            Assert.That(_photos.Photos, Is.Empty);
        });
    }

    // Permission.Denied → nasze wyjaśnienie; po „Zezwól” druga (ostatnia) prośba systemowa i zdjęcie.
    [Test]
    public async Task AddPhoto_Denied_AllowAfterExplanation_AsksAgainAndStoresPhoto()
    {
        _permissions.SetupSequence(p => p.EnsureCameraAsync())
            .ReturnsAsync(Result.Failure(DeviceErrors.PermissionDenied))
            .ReturnsAsync(Result.Success());
        _feedback.Setup(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraExplain", "Permission_Allow", "Permission_NotNow"))
            .ReturnsAsync(true);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _feedback.Verify(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraExplain", "Permission_Allow", "Permission_NotNow"), Times.Once);
            _permissions.Verify(p => p.EnsureCameraAsync(), Times.Exactly(2));
            Assert.That(_photos.Photos, Has.Count.EqualTo(1));
            _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
        });
    }

    // Android po pierwszej odmowie: kolejne „Dodaj zdjęcie” najpierw pokazuje nasze wyjaśnienie, a „Nie teraz”
    // nie wysyła ostatniej prośby systemowej.
    [Test]
    public async Task AddPhoto_ShouldExplain_NotNow_DoesNotAskSystem()
    {
        _permissions.Setup(p => p.ShouldExplainCamera()).Returns(true);
        _feedback.Setup(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraExplain", "Permission_Allow", "Permission_NotNow"))
            .ReturnsAsync(false);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _permissions.Verify(p => p.EnsureCameraAsync(), Times.Never);
            _camera.Verify(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task AddPhoto_ShouldExplain_Allow_AsksSystemAndStoresPhoto()
    {
        _permissions.Setup(p => p.ShouldExplainCamera()).Returns(true);
        _feedback.Setup(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraExplain", "Permission_Allow", "Permission_NotNow"))
            .ReturnsAsync(true);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _permissions.Verify(p => p.EnsureCameraAsync(), Times.Once);
            Assert.That(_photos.Photos, Has.Count.EqualTo(1));
        });
    }

    // „Nie teraz” na naszym ekranie niczego nie spala: brak drugiej prośby systemowej i komunikatu.
    [Test]
    public async Task AddPhoto_Denied_NotNow_DoesNothingMore()
    {
        _permissions.Setup(p => p.EnsureCameraAsync()).ReturnsAsync(Result.Failure(DeviceErrors.PermissionDenied));
        _feedback.Setup(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraExplain", "Permission_Allow", "Permission_NotNow"))
            .ReturnsAsync(false);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _feedback.Verify(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraExplain", "Permission_Allow", "Permission_NotNow"), Times.Once);
            _permissions.Verify(p => p.EnsureCameraAsync(), Times.Once);
            _camera.Verify(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()), Times.Never);
            _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
            _permissions.Verify(p => p.OpenAppSettings(), Times.Never);
        });
    }

    // Permission.DeniedPermanently → wyjaśnienie; po potwierdzeniu otwarcie ustawień systemu.
    [Test]
    public async Task AddPhoto_DeniedPermanently_Confirmed_OpensAppSettings()
    {
        _permissions.Setup(p => p.EnsureCameraAsync()).ReturnsAsync(Result.Failure(DeviceErrors.PermissionDeniedPermanently));
        _feedback.Setup(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraSettings", "Permission_OpenSettings", "Common_Cancel"))
            .ReturnsAsync(true);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _permissions.Verify(p => p.OpenAppSettings(), Times.Once);
            _permissions.Verify(p => p.EnsureCameraAsync(), Times.Once);
            _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
        });
    }

    [Test]
    public async Task AddPhoto_DeniedPermanently_Cancelled_DoesNotOpenSettings()
    {
        _permissions.Setup(p => p.EnsureCameraAsync()).ReturnsAsync(Result.Failure(DeviceErrors.PermissionDeniedPermanently));
        _feedback.Setup(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraSettings", "Permission_OpenSettings", "Common_Cancel"))
            .ReturnsAsync(false);
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            _feedback.Verify(f => f.ConfirmAsync("Permission_CameraTitle", "Permission_CameraSettings", "Permission_OpenSettings", "Common_Cancel"), Times.Once);
            _permissions.Verify(p => p.OpenAppSettings(), Times.Never);
            _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
        });
    }

    // Inny błąd zdjęcia → zwykły komunikat błędu.
    [Test]
    public async Task AddPhoto_OtherError_ShowsError()
    {
        var failure = new Error("Photo.Failed", ErrorType.Unexpected);
        _camera.Setup(c => c.CapturePhotoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(failure));
        await _viewModel.OnNavigatedToAsync(_visitId);

        await _viewModel.AddPhotoCommand.ExecuteAsync(null);

        _feedback.Verify(f => f.ShowErrorAsync(failure), Times.Once);
    }

    // ---------- given ----------

    private void GivenVanStock() =>
        _inventory.Setup(i => i.GetVanStockAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VanStockItem>>.Success(new[]
            {
                new VanStockItem(TestVisits.Filter.Value, "Filtr siatkowy", 3),
                new VanStockItem("P-ZAWOR-02", "Zawór trójdrogowy", 1),
            }));

    private static VisitDetails Details(VisitId visitId) => new(
        visitId.Value, "ZL-2026-0042", VisitStatus.InProgress, FixedClock.DefaultNow, FixedClock.DefaultNow.AddHours(2),
        new CustomerSnapshot(CustomerName, "+48 600 100 200", "ul. Polna 1, Kraków"),
        "Pompa ciepła AW-8", "Nie grzeje wody", "https://example.invalid/thumb.png",
        ["Diagnostyka pompy ciepła"], [], false, null, null, 0);
}
