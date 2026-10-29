using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Visits.Completion;

// Krok 1: wykonane czynności – wybór z katalogu czynności
public sealed partial class ActivitiesStepViewModel(
    CompletionDraft draft,
    RecordActivityHandler record,
    GetVisitDetailsHandler details,
    ILocalizer localizer,
    IUserFeedback feedback) : CompletionStep
{
    // Katalog czynności: kod (do API) → klucz tekstu w Strings.resx
    private static readonly string[] Codes = ["DIAG", "NAPR", "CZYSZ", "TEST"];

    public override string Title => localizer["Completion_ActivitiesTitle"];

    public override string NextButtonText => localizer["Completion_Next"];

    [ObservableProperty]
    public partial IReadOnlyList<string> Activities { get; private set; } = [];

    public override Task OnShownAsync()
    {
        Activities = draft.Details?.Activities ?? [];
        IsValid = true; // zakres może mieć same części
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AddActivityAsync()
    {
        var names = Codes.Select(code => localizer[$"Activity_{code}"]).ToList();
        var chosen = await feedback.ChooseAsync("Completion_ChooseActivity", names);
        var index = chosen is null ? -1 : names.IndexOf(chosen);
        if (index < 0) return;

        if (await ApplyAsync(record.HandleAsync(new RecordActivity(draft.VisitId, Codes[index], names[index]), CancellationToken.None),
                draft, details, feedback))
            await OnShownAsync();
    }
}
