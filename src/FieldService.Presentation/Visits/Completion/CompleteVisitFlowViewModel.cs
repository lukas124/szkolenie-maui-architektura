using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Visits.Completion;

// Kreator zakończenia wizyty: jedna strona, kroki jako ViewModele komponentów, stan przepływu w CompletionDraft.
// Kreator przesuwa kroki i woła przypadek użycia – o poprawności i tekście przycisku decyduje krok.
public sealed partial class CompleteVisitFlowViewModel : BaseViewModel, INavigatedTo<CompleteVisitRoute>
{
    private readonly CompletionDraft _draft;
    private readonly GetVisitDetailsHandler _details;
    private readonly CompleteVisitHandler _complete;
    private readonly IUserFeedback _feedback;
    private readonly INavigationService _navigation;
    private readonly ILocalizer _localizer;

    public CompleteVisitFlowViewModel(CompletionDraft draft, ActivitiesStepViewModel activities, PartsStepViewModel parts,
        SignatureStepViewModel signature, SummaryStepViewModel summary, GetVisitDetailsHandler details,
        CompleteVisitHandler complete, IUserFeedback feedback, INavigationService navigation, ILocalizer localizer)
    {
        _draft = draft;
        _details = details;
        _complete = complete;
        _feedback = feedback;
        _navigation = navigation;
        _localizer = localizer;
        Title = localizer["Completion_Title"];
        Steps = [activities, parts, signature, summary];
        CurrentStep = activities;
    }

    public string Title { get; }

    public IReadOnlyList<CompletionStep> Steps { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(StepText))]
    public partial int CurrentIndex { get; private set; }

    // „KROK 2 Z 4” – liczba kroków z listy (nowy krok nie wymaga zmiany tekstu)
    public string StepText => string.Format(_localizer["Completion_StepOf"], CurrentIndex + 1, Steps.Count);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial CompletionStep CurrentStep { get; private set; }

    public bool CanGoBack => CurrentIndex > 0;

    public async Task OnNavigatedToAsync(CompleteVisitRoute route)
    {
        _draft.VisitId = route.Id;
        var details = await _details.HandleAsync(new GetVisitDetails(route.Id), CancellationToken.None);
        if (!details.IsSuccess)
        {
            await _feedback.ShowErrorAsync(details.Error!);
            await _navigation.CloseModalAsync(); // pusty kreator nic nie da – wracamy do szczegółów
            return;
        }

        _draft.Details = details.Value;
        await ShowAsync(0);
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        if (!await CurrentStep.TryLeaveAsync()) return;

        if (CurrentIndex == Steps.Count - 1) await FinishAsync();
        else await ShowAsync(CurrentIndex + 1);
    }

    private bool CanGoNext() => CurrentStep.IsValid;

    [RelayCommand]
    private Task BackAsync() => CanGoBack ? ShowAsync(CurrentIndex - 1) : Task.CompletedTask;

    // Przerwanie kreatora: to, co technik już zapisał w krokach (czynności, części), zostaje w wizycie
    [RelayCommand]
    private Task CancelAsync() => _navigation.CloseModalAsync();

    // Ostatni krok: przypadek użycia zakończenia wizyty, po zapisie zamykamy kreator i wracamy na listę
    private async Task FinishAsync()
    {
        var result = await _complete.HandleAsync(new CompleteVisit(_draft.VisitId, VisitOutcomeType.Fixed, null), CancellationToken.None);
        if (!result.IsSuccess)
        {
            await _feedback.ShowErrorAsync(result.Error!);
            return;
        }

        await _navigation.CloseModalAsync();
        await _navigation.GoBackAsync();
    }

    private async Task ShowAsync(int index)
    {
        CurrentStep.PropertyChanged -= OnStepChanged;
        CurrentIndex = index;
        CurrentStep = Steps[index];
        CurrentStep.PropertyChanged += OnStepChanged;
        await CurrentStep.OnShownAsync();
        NextCommand.NotifyCanExecuteChanged();
    }

    private void OnStepChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompletionStep.IsValid)) NextCommand.NotifyCanExecuteChanged();
    }

    // Kreator zamknięty – kroki są w zakresie przepływu, ale odpinamy subskrypcję jawnie
    public override void OnClosed() => CurrentStep.PropertyChanged -= OnStepChanged;
}
