using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Messages;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.State;

namespace FieldService.Presentation.Visits;

// Lista wizyt na dziś. ViewModel koordynuje: przypadek użycia → Result → fabryka → kolekcja albo komunikat błędu.
// Nie formatuje, nie buduje elementów, nie składa tekstów i nie zna stron. Filtr bierze ze stanu aplikacji (IAppState),
// a o jego zmianie dowiaduje się z komunikatu – słaby posłaniec nie trzyma listy w pamięci po zamknięciu ekranu.
// Pokazuje też, ile zmian czeka na wysłanie – nie obiecujemy wysyłki w tle, dajemy „Wyślij teraz”.
public partial class VisitsListViewModel : BaseViewModel, INavigatedTo<VisitsListRoute>, IRecipient<FilterChangedMessage>,
    IRecipient<SyncCompletedMessage>
{
    private readonly GetTodayVisitsHandler _getTodayVisits;
    private readonly IVisitItemFactory _items;
    private readonly IVisitFormatter _formatter;
    private readonly IUserFeedback _feedback;
    private readonly INavigationService _navigation;
    private readonly IAppState _appState;
    private readonly ISyncScheduler _sync;

    public VisitsListViewModel(GetTodayVisitsHandler getTodayVisits, IVisitItemFactory items, IVisitFormatter formatter,
        IUserFeedback feedback, INavigationService navigation, IAppState appState, ISyncScheduler sync)
    {
        _getTodayVisits = getTodayVisits;
        _items = items;
        _formatter = formatter;
        _feedback = feedback;
        _navigation = navigation;
        _appState = appState;
        _sync = sync;
        // wszystkie komunikaty, które lista odbiera (IRecipient<…>)
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<VisitItem> Visits { get; } = [];

    // gest „pociągnij, aby odświeżyć” (RefreshView sam ustawia true)
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    public partial string LastUpdated { get; set; } = "";

    // zmiany z telefonu, które jeszcze nie dotarły na serwer
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending), nameof(PendingText))]
    public partial int PendingCount { get; set; }

    public bool HasPending => PendingCount > 0;

    public string PendingText => _formatter.FormatPending(PendingCount);

    // true = lista była już załadowana (wejście na ekran), kolejne pojawienie się = powrót z innej strony
    private bool _loaded;

    public async Task OnNavigatedToAsync(VisitsListRoute route)
    {
        await LoadAsync(CancellationToken.None);
        _loaded = true;
    }

    // true = lista otworzyła szczegóły wizyty. Po powrocie technik mógł zmienić wizytę (status z repliki)
    private bool _returningFromDetails;

    // Powrót ze szczegółów – lista od nowa. Po zamknięciu filtra przeładowuje komunikat, nie pojawienie się strony
    public override Task OnAppearingAsync()
    {
        if (!_loaded || !_returningFromDetails) return Task.CompletedTask;
        _returningFromDetails = false;
        return LoadAsync(CancellationToken.None);
    }

    // Lista zdjęta ze stosu (np. reset po ponownym logowaniu) – bez odbierania komunikatów
    public override void OnClosed() => WeakReferenceMessenger.Default.UnregisterAll(this);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private Task OpenVisitAsync(VisitItem item)
    {
        _returningFromDetails = true;
        return _navigation.NavigateAsync(new VisitDetailsRoute(new VisitId(item.Id)));
    }

    [RelayCommand]
    private void SendNow() => _sync.RequestSync();

    [RelayCommand]
    private Task OpenFilterAsync() => _navigation.OpenModalAsync(new VisitFilterRoute());

    [RelayCommand]
    private Task OpenWorkOrdersAsync() => _navigation.NavigateAsync(new WorkOrdersRoute());

    // true = lista pokazuje tylko część wizyt (widać to na ekranie)
    public bool IsFiltered => _appState.Filter.OnlyOverdue || _appState.Filter.Status is not null;

    public void Receive(FilterChangedMessage message)
    {
        OnPropertyChanged(nameof(IsFiltered));
        // komunikat przychodzi synchronicznie, ładowanie sam obsługuje błędy przez Result i IUserFeedback
        _ = LoadAsync(CancellationToken.None);
    }

    // Przebieg wysyłki skończony: nowy licznik. Po wysłaniu albo odrzuceniu zmian lista od nowa (statusy z serwera)
    public void Receive(SyncCompletedMessage message)
    {
        var result = message.Result;
        PendingCount = result.Pending;
        if (result.Conflicts.Count > 0) _ = _feedback.ShowErrorAsync(SyncErrors.Conflict);
        else if (result.Rejected.Count > 0) _ = _feedback.ShowErrorAsync(SyncErrors.Rejected);
        if (result.Sent > 0 || result.Conflicts.Count > 0 || result.Rejected.Count > 0) _ = LoadAsync(CancellationToken.None);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        // licznik z bazy telefonu – aktualny także bez sieci
        PendingCount = await _sync.GetPendingCountAsync(ct);

        var filter = _appState.Filter;
        var result = await _getTodayVisits.HandleAsync(new GetTodayVisits(filter.OnlyOverdue, filter.Status), ct);
        if (!result.IsSuccess)
        {
            await _feedback.ShowErrorAsync(result.Error!);
            return;
        }

        var today = result.Value;
        Visits.ReplaceWith(today.Visits.Select(_items.Create));
        Summary = _formatter.FormatDaySummary(today.Visits);
        // bez sieci: lista z migawki – technik widzi, z kiedy są dane
        LastUpdated = today.OfflineSince is { } since ? _formatter.FormatOfflineSince(since) : _formatter.FormatUpdatedNow();
    }
}
