using FieldService.Application.Abstractions;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Session;

// Po starcie (zalogowaniu) odtwarzamy cel, a nie cały stos: niedokończony kreator → pytanie „Wróć do wizyty?”.
// Najpierw prośba o wysyłkę zmian, które czekały na sesję (np. po ubiciu aplikacji bez zasięgu).
public sealed class StartupNavigator(IFlowDraftStore drafts, IUserFeedback feedback, INavigationService navigation,
    ISyncScheduler sync)
{
    public async Task ResumeAsync()
    {
        sync.RequestSync();
        var draft = await drafts.GetAsync(CancellationToken.None);
        if (draft is null) return;

        if (await feedback.ConfirmAsync("Startup_ResumeTitle", "Startup_ResumeMessage", "Startup_Resume", "Startup_Discard"))
            await navigation.OpenModalAsync(new CompleteVisitRoute(draft.VisitId));
        else
            await drafts.ClearAsync(CancellationToken.None);
    }
}
