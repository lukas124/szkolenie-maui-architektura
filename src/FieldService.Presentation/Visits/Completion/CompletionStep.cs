using CommunityToolkit.Mvvm.ComponentModel;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Visits.Completion;

// Krok kreatora (ViewModel komponentu). Krok sam mówi, czy jest poprawny i jaki ma tekst przycisku „Dalej” –
// kreator nie pyta o typ kroku (Strategia, zasada otwarte–zamknięte: nowy krok = nowa klasa).
public abstract partial class CompletionStep : ObservableObject
{
    public abstract string Title { get; }

    public abstract string NextButtonText { get; }

    // „Dalej” aktywne tylko dla poprawnego kroku
    [ObservableProperty]
    public partial bool IsValid { get; protected set; }

    // Krok staje się widoczny – dane z CompletionDraft mogły się zmienić w innym kroku
    public virtual Task OnShownAsync() => Task.CompletedTask;

    // Przed przejściem dalej; false = zostań na kroku (np. zapis się nie udał)
    public virtual Task<bool> TryLeaveAsync() => Task.FromResult(true);

    // Wspólne dla kroków, które zmieniają zakres: po udanej komendzie świeże szczegóły z repliki
    protected static async Task<bool> ApplyAsync(Task<Result> command, CompletionDraft draft,
        GetVisitDetailsHandler details, IUserFeedback feedback)
    {
        var result = await command;
        if (!result.IsSuccess)
        {
            await feedback.ShowErrorAsync(result.Error!);
            // wizyta zmieniła się w międzyczasie (albo serwer odrzucił zmiany z telefonu) – krok pokazuje stan z serwera
            if (result.Error!.Type is ErrorType.Conflict or ErrorType.NotFound) await RefreshAsync(draft, details);
            return false;
        }

        await RefreshAsync(draft, details);
        return true;
    }

    private static async Task RefreshAsync(CompletionDraft draft, GetVisitDetailsHandler details)
    {
        var fresh = await details.HandleAsync(new GetVisitDetails(draft.VisitId), CancellationToken.None);
        if (fresh.IsSuccess) draft.Details = fresh.Value;
    }
}
