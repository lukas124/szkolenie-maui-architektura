using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Session;

// Po starcie (zalogowaniu albo przywróceniu sesji) odtwarzamy cel, a nie cały stos: wizyta z linku, który otworzył
// aplikację, potem niedokończony kreator → pytanie „Wróć do wizyty?”. Najpierw prośba o wysyłkę zmian, które czekały
// na sesję (np. po ubiciu aplikacji bez zasięgu). Singleton – link zapamiętany przed zalogowaniem czeka na nie.
public sealed class StartupNavigator(IFlowDraftStore drafts, IUserFeedback feedback, INavigationService navigation,
    ISyncScheduler sync)
{
    private VisitId? _linkedVisit;

    // Deep link przy zimnym starcie (jeszcze bez sesji) – otworzymy go po zalogowaniu
    public void RememberVisit(VisitId id) => _linkedVisit = id;

    public async Task ResumeAsync()
    {
        sync.RequestSync();
        if (_linkedVisit is { } linked)
        {
            _linkedVisit = null;
            await navigation.NavigateAsync(new VisitDetailsRoute(linked));
        }

        var draft = await drafts.GetAsync(CancellationToken.None);
        if (draft is null) return;

        if (await feedback.ConfirmAsync("Startup_ResumeTitle", "Startup_ResumeMessage", "Startup_Resume", "Startup_Discard"))
            await navigation.OpenModalAsync(new CompleteVisitRoute(draft.VisitId));
        else
            await drafts.ClearAsync(CancellationToken.None);
    }
}
