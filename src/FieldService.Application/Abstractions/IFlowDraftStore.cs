using FieldService.Domain.Visits;

namespace FieldService.Application.Abstractions;

// Szkic kreatora, który musi przeżyć ubicie procesu przez system: która wizyta i na którym kroku
public sealed record FlowDraft(VisitId VisitId, int StepIndex);

// Port: zapis szkicu od razu po zmianie (po każdym kroku), nie dopiero przy zamykaniu okna – może nie zdążyć
public interface IFlowDraftStore
{
    Task SaveAsync(FlowDraft draft, CancellationToken ct);

    // null = brak niedokończonego kreatora
    Task<FlowDraft?> GetAsync(CancellationToken ct);

    Task ClearAsync(CancellationToken ct);
}
