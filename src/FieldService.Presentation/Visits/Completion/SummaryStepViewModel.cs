using CommunityToolkit.Mvvm.ComponentModel;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Protocols;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Visits.Completion;

// Krok 4: protokół z bieżącego zakresu (transformata VisitProtocolBuilder) – ostatni krok, przycisk „Zakończ wizytę”
public sealed partial class SummaryStepViewModel(
    CompletionDraft draft,
    GetVisitProtocolHandler protocol,
    IUserFeedback feedback,
    ILocalizer localizer) : CompletionStep
{
    public override string Title => localizer["Completion_SummaryTitle"];

    public override string NextButtonText => localizer["Completion_Finish"];

    [ObservableProperty]
    public partial VisitProtocol? Protocol { get; private set; }

    public override async Task OnShownAsync()
    {
        var result = await protocol.HandleAsync(new GetVisitProtocol(draft.VisitId), CancellationToken.None);
        if (result.IsSuccess) Protocol = result.Value;
        else await feedback.ShowErrorAsync(result.Error!);
        IsValid = Protocol is not null;
    }
}
