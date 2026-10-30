using System.Runtime.CompilerServices;
using FieldService.Domain.Visits;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits.Completion;

namespace FieldService.Presentation.Tests.Visits.Completion;

// Kreator zakończenia wizyty do testów komunikatu wysyłki i LeakTests – zbudowany przez CompletionFixture (wizyta
// w toku z czynnością).
internal sealed class CompleteVisitFlowFixture
{
    // kreator → jego fixture (słabe klucze: tabela nie trzyma kreatora przy życiu – LeakTests)
    private static readonly ConditionalWeakTable<CompleteVisitFlowViewModel, CompletionFixture> Fixtures = new();

    // tylko atrapa nawigacji i Id wizyty (nie cały fixture) – fixture nie może trzymać kreatora przy życiu (LeakTests)
    public Mock<INavigationService> Navigation { get; private set; } = new();

    public VisitId VisitId { get; private set; }

    public CompleteVisitFlowViewModel CreateViewModel()
    {
        var completion = new CompletionFixture();
        completion.SeedInProgressVisit();
        completion.RecordActivityAsync("NAPR", "Naprawa").GetAwaiter().GetResult();
        completion.CurrentDetails = completion.Details(activities: ["Naprawa"]);
        Navigation = completion.Navigation;
        VisitId = completion.VisitId;
        Fixtures.AddOrUpdate(completion.Flow, completion);
        return completion.Flow;
    }

    public static Task StartAsync(CompleteVisitFlowViewModel viewModel) =>
        viewModel.OnNavigatedToAsync(new CompleteVisitRoute(Fixtures.GetValue(viewModel, _ => throw new InvalidOperationException()).VisitId));

    // czynności → części → podpis (narysowany) → podsumowanie
    public static async Task MoveToSummaryAsync(CompleteVisitFlowViewModel viewModel)
    {
        var completion = Fixtures.GetValue(viewModel, _ => throw new InvalidOperationException());
        await viewModel.NextCommand.ExecuteAsync(null);
        await viewModel.NextCommand.ExecuteAsync(null);
        completion.Signature.HasDrawing = true;
        await viewModel.NextCommand.ExecuteAsync(null);
        if (viewModel.CurrentStep is not SummaryStepViewModel)
            throw new InvalidOperationException("Nie udało się przejść do kroku podsumowania.");
    }
}
