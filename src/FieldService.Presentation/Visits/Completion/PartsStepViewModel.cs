using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Inventory;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Visits.Completion;

// Krok 2: zużyte części – dodanie z auta technika (1 szt.) i usunięcie omyłkowo dopisanej (R7)
public sealed partial class PartsStepViewModel(
    CompletionDraft draft,
    UsePartHandler usePart,
    RemoveUsedPartHandler removePart,
    GetVanStockHandler vanStock,
    GetVisitDetailsHandler details,
    IUserFeedback feedback,
    ILocalizer localizer) : CompletionStep
{
    public override string Title => localizer["Completion_PartsTitle"];

    public override string NextButtonText => localizer["Completion_Next"];

    [ObservableProperty]
    public partial IReadOnlyList<VisitPartLine> Parts { get; private set; } = [];

    public override Task OnShownAsync()
    {
        Parts = draft.Details?.Parts ?? [];
        IsValid = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AddPartAsync()
    {
        var stock = await vanStock.HandleAsync(new GetVanStock(), CancellationToken.None);
        if (!stock.IsSuccess)
        {
            await feedback.ShowErrorAsync(stock.Error!);
            return;
        }

        var chosen = await feedback.ChooseAsync("Visit_ChoosePart", stock.Value.Select(p => p.Name).ToList());
        var part = stock.Value.FirstOrDefault(p => p.Name == chosen);
        if (part is null) return;

        if (await ApplyAsync(usePart.HandleAsync(new UsePart(draft.VisitId, new PartId(part.PartId), 1), CancellationToken.None),
                draft, details, feedback))
            await OnShownAsync();
    }

    [RelayCommand]
    private async Task RemovePartAsync(VisitPartLine line)
    {
        if (await ApplyAsync(removePart.HandleAsync(new RemoveUsedPart(draft.VisitId, new PartId(line.PartId)), CancellationToken.None),
                draft, details, feedback))
            await OnShownAsync();
    }
}
