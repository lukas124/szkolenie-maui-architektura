using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Visits;

// Szczegóły wizyty i akcje technika. Każda akcja: przypadek użycia → Result → ponowne załadowanie albo komunikat.
// Sygnał ostrzegawczy: 10 zależności – w checkpoincie 09-navigation kroki kreatora zakończenia przejmą części, podpis i zakończenie.
public partial class VisitDetailsViewModel(
    GetVisitDetailsHandler getDetails,
    StartVisitHandler startVisit,
    UsePartHandler usePart,
    SignByCustomerHandler sign,
    CompleteVisitHandler complete,
    AddVisitPhotoHandler addPhoto,
    GetVanStockHandler getVanStock,
    IPermissionService permissions,
    IVisitFormatter formatter,
    IUserFeedback feedback) : BaseViewModel
{
    private VisitId _visitId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(TimeWindowText), nameof(CanStart), nameof(IsInProgress), nameof(SignatureText))]
    public partial VisitDetails? Details { get; set; }

    public string StatusText => Details is null ? "" : formatter.FormatStatus(Details.Status, isLate: false);

    public string TimeWindowText => Details is null ? "" : formatter.FormatTimeWindow(Details.PlannedStart, Details.PlannedEnd);

    public bool CanStart => Details?.Status == VisitStatus.Planned;

    public bool IsInProgress => Details?.Status == VisitStatus.InProgress;

    public string? SignatureText => Details?.HasValidSignature == true ? Details.SignerName : null;

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is VisitId id) _visitId = id;
        await LoadAsync(refresh: true);
    }

    [RelayCommand]
    private Task StartAsync() => RunAsync(() => startVisit.HandleAsync(new StartVisit(_visitId), CancellationToken.None));

    // Część z auta technika – wybór z listy, ilość 1 (dokładniejszy wybór w kreatorze zakończenia wizyty)
    [RelayCommand]
    private async Task AddPartAsync(CancellationToken ct)
    {
        var stock = await getVanStock.HandleAsync(new GetVanStock(), ct);
        if (!stock.IsSuccess)
        {
            await feedback.ShowErrorAsync(stock.Error!);
            return;
        }

        var names = stock.Value.Select(p => p.Name).ToList();
        var chosen = await feedback.ChooseAsync("Visit_ChoosePart", names);
        var part = stock.Value.FirstOrDefault(p => p.Name == chosen);
        if (part is null) return;

        await RunAsync(() => usePart.HandleAsync(new UsePart(_visitId, new PartId(part.PartId), 1), ct));
    }

    // TODO: Ćwiczenie 08 – R7: [RelayCommand] RemovePartAsync(VisitPartLine line, CancellationToken ct) przez RunAsync
    // i RemoveUsedPartHandler (parametr konstruktora); w VisitDetailsPage.xaml przycisk „Usuń” przy pozycji części

    [RelayCommand]
    private async Task SignAsync(CancellationToken ct)
    {
        if (Details is null || !await feedback.ConfirmAsync("Visit_SignTitle", "Visit_SignMessage")) return;
        await RunAsync(() => sign.HandleAsync(new SignByCustomer(_visitId, Details.Customer.Name), ct));
    }

    [RelayCommand]
    private Task CompleteAsync(CancellationToken ct) =>
        RunAsync(() => complete.HandleAsync(new CompleteVisit(_visitId, VisitOutcomeType.Fixed, null), ct));

    // Android po pierwszej odmowie pokaże okno zgody jeszcze raz – ostatni. Najpierw nasze wyjaśnienie
    [RelayCommand]
    private async Task AddPhotoAsync(CancellationToken ct)
    {
        if (permissions.ShouldExplainCamera() && !await ExplainCameraAsync()) return;

        var result = await addPhoto.HandleAsync(new AddVisitPhoto(_visitId), ct);
        if (result.IsSuccess) await LoadAsync();
        else await HandlePhotoErrorAsync(result.Error!, ct);
    }

    // Trzy reakcje na błąd zdjęcia: anulowanie to nie błąd, odmowa – nasze wyjaśnienie przed ostatnią systemową
    // prośbą („Nie teraz” niczego nie spala), odmowa na stałe – wyjaśnienie i Ustawienia systemu
    private async Task HandlePhotoErrorAsync(Error error, CancellationToken ct)
    {
        if (error == DeviceErrors.PhotoCancelled) return;

        if (error == DeviceErrors.PermissionDenied)
        {
            if (!await ExplainCameraAsync()) return;
            var retry = await addPhoto.HandleAsync(new AddVisitPhoto(_visitId), ct);
            if (retry.IsSuccess) await LoadAsync();
            else if (retry.Error != DeviceErrors.PhotoCancelled) await HandleFinalPhotoErrorAsync(retry.Error!);
            return;
        }

        await HandleFinalPhotoErrorAsync(error);
    }

    private Task<bool> ExplainCameraAsync() =>
        feedback.ConfirmAsync("Permission_CameraTitle", "Permission_CameraExplain", "Permission_Allow", "Permission_NotNow");

    private async Task HandleFinalPhotoErrorAsync(Error error)
    {
        if (error == DeviceErrors.PermissionDeniedPermanently || error == DeviceErrors.PermissionDenied)
        {
            if (await feedback.ConfirmAsync("Permission_CameraTitle", "Permission_CameraSettings", "Permission_OpenSettings", "Common_Cancel"))
                permissions.OpenAppSettings();
            return;
        }

        await feedback.ShowErrorAsync(error);
    }

    // Typ błędu decyduje o reakcji: konflikt (ktoś zmienił wizytę) → komunikat i świeże dane, inne → komunikat
    private async Task RunAsync(Func<Task<Result>> command)
    {
        var result = await command();
        if (result.IsSuccess)
        {
            await LoadAsync();
            return;
        }

        await feedback.ShowErrorAsync(result.Error!);
        if (result.Error!.Type == ErrorType.Conflict) await LoadAsync();
    }

    private async Task LoadAsync(bool refresh = false)
    {
        var result = await getDetails.HandleAsync(new GetVisitDetails(_visitId, refresh), CancellationToken.None);
        if (result.IsSuccess) Details = result.Value;
        else await feedback.ShowErrorAsync(result.Error!);
    }
}
