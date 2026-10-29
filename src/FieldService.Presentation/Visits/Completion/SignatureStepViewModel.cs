using CommunityToolkit.Mvvm.ComponentModel;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Visits.Completion;

// Krok 3: podpis klienta pod bieżącym zakresem (R4). Rysunek zbiera kontrolka podpisu w UI – ViewModel wie tylko,
// czy klient coś narysował (HasDrawing).
public sealed partial class SignatureStepViewModel(
    CompletionDraft draft,
    SignByCustomerHandler sign,
    GetVisitDetailsHandler details,
    IUserFeedback feedback,
    ILocalizer localizer) : CompletionStep
{
    public override string Title => localizer["Completion_SignatureTitle"];

    public override string NextButtonText => localizer["Completion_Next"];

    [ObservableProperty]
    public partial string SignerName { get; set; } = "";

    [ObservableProperty]
    public partial bool HasDrawing { get; set; }

    public override Task OnShownAsync()
    {
        // Pole podpisu w UI jest przy każdym pokazaniu kroku puste – klient podpisuje bieżący zakres od nowa (R3)
        HasDrawing = false;
        if (SignerName.Length == 0) SignerName = draft.Details?.Customer.Name ?? "";
        Validate();
        return Task.CompletedTask;
    }

    public override Task<bool> TryLeaveAsync() =>
        ApplyAsync(sign.HandleAsync(new SignByCustomer(draft.VisitId, SignerName.Trim()), CancellationToken.None), draft, details, feedback);

    partial void OnSignerNameChanged(string value) => Validate();

    partial void OnHasDrawingChanged(bool value) => Validate();

    // Podpis ma sens tylko pod niepustym zakresem (R4) – reguła i tak jest w Visit, tu tylko blokada przycisku
    private void Validate() =>
        IsValid = SignerName.Trim().Length > 0 && HasDrawing
                  && (draft.Details?.Activities.Count > 0 || draft.Details?.Parts.Count > 0);
}
