using FieldService.Presentation.Feedback;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Messages;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.State;

namespace FieldService.Presentation.Visits;

// Filtr listy wizyt (modal): stan ekranu do „Zastosuj”, potem stan aplikacji i komunikat dla listy
public sealed partial class VisitFilterViewModel(IAppState appState, INavigationService navigation, IUserFeedback feedback,
    ILocalizer localizer)
    : BaseViewModel, INavigatedTo<VisitFilterRoute>
{
    [ObservableProperty]
    public partial bool OnlyOverdue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial VisitStatus? Status { get; set; }

    // tekst przycisku wyboru statusu: „Wszystkie”, „W trakcie”…
    public string StatusText => StatusName(Status);

    // null = wszystkie statusy
    public IReadOnlyList<VisitStatus?> Statuses { get; } =
        [null, VisitStatus.Planned, VisitStatus.InProgress, VisitStatus.Completed, VisitStatus.Cancelled];

    public Task OnNavigatedToAsync(VisitFilterRoute route)
    {
        OnlyOverdue = appState.Filter.OnlyOverdue;
        Status = appState.Filter.Status;
        return Task.CompletedTask;
    }

    // wybór statusu – lista jako strona modalna (IUserFeedback.ChooseAsync), nie systemowy Picker
    [RelayCommand]
    private async Task ChooseStatusAsync()
    {
        var names = Statuses.Select(StatusName).ToList();
        var chosen = await feedback.ChooseAsync("Visits_ChooseStatus", names);
        if (chosen is not null) Status = Statuses[names.IndexOf(chosen)];
    }

    [RelayCommand]
    private Task ApplyAsync() => ApplyAsync(OnlyOverdue, Status);

    [RelayCommand]
    private Task ClearAsync() => ApplyAsync(false, null);

    private Task ApplyAsync(bool onlyOverdue, VisitStatus? status)
    {
        appState.ApplyFilter(onlyOverdue, status);
        WeakReferenceMessenger.Default.Send(new FilterChangedMessage());
        return navigation.CloseModalAsync();
    }

    private string StatusName(VisitStatus? status) => localizer[status is { } s ? $"Visits_Status_{s}" : "Visits_Status_All"];
}
