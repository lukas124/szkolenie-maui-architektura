using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.State;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Visits;

// Szczegóły wizyty: rozpoczęcie, zdjęcie i wejście w kreator zakończenia (części, podpis i zakończenie są w kreatorze).
// Po wysyłce zmian szczegóły od nowa (zdarzenie AppState.SyncCompleted).
public partial class VisitDetailsViewModel(
    GetVisitDetailsHandler getDetails,
    StartVisitHandler startVisit,
    AddVisitPhotoHandler addPhoto,
    IPermissionService permissions,
    IVisitFormatter formatter,
    IUserFeedback feedback,
    INavigationService navigation) : BaseViewModel, INavigatedTo<VisitDetailsRoute>
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

    // true = szczegóły były już załadowane, kolejne pojawienie się = powrót z kreatora
    private bool _loaded;

    public async Task OnNavigatedToAsync(VisitDetailsRoute route)
    {
        _visitId = route.Id;
        AppState.SyncCompleted += OnSyncCompleted;
        await LoadAsync(refresh: true);
        _loaded = true;
    }

    // wysyłka zmian skończona – status wizyty mógł się zmienić (wysłana, odrzucona, konflikt)
    private void OnSyncCompleted(object? sender, EventArgs e)
    {
        if (_loaded) _ = LoadAsync();
    }

    // Powrót z kreatora (przerwanego): zakres i podpis mogły się zmienić – świeże dane z repliki
    public override Task OnAppearingAsync() => _loaded ? LoadAsync() : Task.CompletedTask;

    [RelayCommand]
    private Task StartAsync() => RunAsync(() => startVisit.HandleAsync(new StartVisit(_visitId), CancellationToken.None));

    // Kreator zakończenia: ViewModel prosi o cel, nie składa obiektów – kreator ma własny zakres (CompletionDraft)
    [RelayCommand]
    private Task CompleteAsync() => navigation.OpenModalAsync(new CompleteVisitRoute(_visitId));

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
        // wizyta zmieniła się w międzyczasie albo serwer odrzucił zmiany z telefonu (lokalna kopia usunięta) – stan z serwera
        if (result.Error!.Type is ErrorType.Conflict or ErrorType.NotFound) await LoadAsync();
    }

    private async Task LoadAsync(bool refresh = false)
    {
        var result = await getDetails.HandleAsync(new GetVisitDetails(_visitId, refresh), CancellationToken.None);
        if (result.IsSuccess) Details = result.Value;
        else await feedback.ShowErrorAsync(result.Error!);
    }
}
