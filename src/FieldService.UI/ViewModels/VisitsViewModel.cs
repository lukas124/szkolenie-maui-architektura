using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Windows.Input;
using FieldService.UI.Helpers;
using FieldService.UI.Models;
using FieldService.UI.Services;
using SQLite;

namespace FieldService.UI.ViewModels;

// Lista wizyt na dziś
public class VisitsViewModel : BaseViewModel
{
    // Inne ekrany zgłaszają tu, że wizyty się zmieniły (np. technik rozpoczął wizytę)
    public static event EventHandler? VisitsChanged;

    public static void RaiseVisitsChanged() => VisitsChanged?.Invoke(null, EventArgs.Empty);

    private readonly HttpClient _http;
    private readonly SQLiteAsyncConnection _db;
    private readonly IVisitStore _store;
    private readonly ApiSession _session;

    private ObservableCollection<VisitItem> _visits = new();
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _isOffline;
    private string _lastUpdated = "";
    private string _summary = "";

    public VisitsViewModel()
    {
        _session = ServiceHelper.GetService<ApiSession>();
        _store = ServiceHelper.GetService<IVisitStore>();
        _http = new HttpClient { BaseAddress = new Uri(_session.BaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        _db = new SQLiteAsyncConnection(Path.Combine(FileSystem.AppDataDirectory, "fieldservice.db3"));

        LoadCommand = new Command(() => LoadData());
        VisitsChanged += (_, _) => LoadData();
    }

    public ObservableCollection<VisitItem> Visits
    {
        get => _visits;
        set => SetProperty(ref _visits, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    // gest „pociągnij, aby odświeżyć” (RefreshView sam ustawia true)
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public bool IsOffline
    {
        get => _isOffline;
        set => SetProperty(ref _isOffline, value);
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public ICommand LoadCommand { get; }

    public async void LoadData()
    {
        IsLoading = true;
        try
        {
            await _db.CreateTableAsync<VisitRow>();
            await _db.CreateTableAsync<PendingStart>();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);

            // najpierw wysyłamy to, co technik zrobił bez internetu
            var pending = await _db.Table<PendingStart>().ToListAsync();
            foreach (var start in pending)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/visits/{start.VisitId}/start")
                {
                    Content = JsonContent.Create(new { startedAt = start.StartedAt })
                };
                request.Headers.TryAddWithoutValidation("If-Match", $"\"{start.Version}\"");
                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
                    await _db.DeleteAsync(start);
            }

            var dtos = await _http.GetFromJsonAsync<List<VisitDto>>("api/visits/today") ?? new List<VisitDto>();
            _store.SetVisits(dtos);

            // zapis w telefonie na wypadek braku internetu
            await _db.DeleteAllAsync<VisitRow>();
            await _db.InsertAllAsync(dtos.Select(d => new VisitRow
            {
                Id = d.Id.ToString(),
                WorkOrderNumber = d.WorkOrderNumber,
                Status = d.Status,
                PlannedStart = d.PlannedStart.LocalDateTime,
                PlannedEnd = d.PlannedEnd.LocalDateTime,
                CustomerName = d.CustomerName,
                Address = d.Address,
                DeviceName = d.DeviceName,
                ThumbnailUrl = d.ThumbnailUrl,
                Version = d.Version
            }));

            var items = new List<VisitItem>();
            foreach (var d in dtos)
            {
                items.Add(CreateItem(d.Id, d.CustomerName, d.Address, d.DeviceName, d.Status,
                    d.PlannedStart.LocalDateTime, d.PlannedEnd.LocalDateTime, d.ThumbnailUrl, d.IsBrigade));
            }

            Visits = new ObservableCollection<VisitItem>(items);
            IsOffline = false;
            LastUpdated = "Aktualizacja: " + DateTime.Now.ToString("dd.MM HH:mm");
            Summary = BuildSummary(dtos.Select(d => d.Status).ToList());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or WebException or IOException)
        {
            // brak internetu albo serwer nie odpowiada
            // (Android zgłasza brak sieci także jako WebException albo IOException): pokazujemy wizyty zapisane w telefonie
            var rows = await _db.Table<VisitRow>().OrderBy(r => r.PlannedStart).ToListAsync();
            var items = new List<VisitItem>();
            foreach (var r in rows)
            {
                items.Add(CreateItem(Guid.Parse(r.Id), r.CustomerName, r.Address, r.DeviceName, r.Status,
                    r.PlannedStart, r.PlannedEnd, r.ThumbnailUrl, false));
            }

            Visits = new ObservableCollection<VisitItem>(items);
            IsOffline = true;
            Summary = BuildSummary(rows.Select(r => r.Status).ToList());
            await Microsoft.Maui.Controls.Application.Current!.Windows[0].Page!
                .DisplayAlertAsync("Brak połączenia", "Pokazuję wizyty zapisane w telefonie. " + ex.Message, "OK");
        }
        catch (Exception ex)
        {
            await Microsoft.Maui.Controls.Application.Current!.Windows[0].Page!
                .DisplayAlertAsync("Błąd", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    private VisitItem CreateItem(Guid id, string customer, string address, string device, string status,
        DateTime start, DateTime end, string thumbnailUrl, bool isBrigade)
    {
        var item = new VisitItem
        {
            Id = id,
            Title = customer,
            Subtitle = $"{device} · {address}",
            IsBrigade = isBrigade,
            Avatar = string.IsNullOrEmpty(thumbnailUrl) ? null : ImageSource.FromUri(new Uri(thumbnailUrl))
        };

        switch (status)
        {
            case "Planned":
                item.StatusText = "Zaplanowana";
                item.StatusColor = Colors.SteelBlue;
                break;
            case "InProgress":
                item.StatusText = "W trakcie";
                item.StatusColor = Colors.Orange;
                break;
            case "Completed":
                item.StatusText = "Zakończona";
                item.StatusColor = Colors.SeaGreen;
                break;
            case "Cancelled":
                item.StatusText = "Anulowana";
                item.StatusColor = Colors.Gray;
                break;
            default:
                item.StatusText = status;
                item.StatusColor = Colors.Gray;
                break;
        }

        // spóźniona = zaplanowana, a okno już się zaczęło
        if (status == "Planned" && DateTime.Now > start)
        {
            item.StatusText = "Spóźniona";
            item.StatusColor = Colors.Red;
        }

        // dzień wizyty: „dziś”, „jutro” albo dzień tygodnia z datą
        var day = start.ToString("ddd dd.MM");
        if (start.Date == DateTime.Now.Date) day = "dziś";
        else if (start.Date == DateTime.Now.Date.AddDays(1)) day = "jutro";

        var minutes = (int)(start - DateTime.Now).TotalMinutes;
        if (status == "Planned" && minutes > 0 && minutes < 60)
            item.TimeText = $"{day} {start.ToString("H:mm")}–{end.ToString("H:mm")} · za {minutes} min";
        else
            item.TimeText = $"{day} {start.ToString("H:mm")}–{end.ToString("H:mm")}";

        return item;
    }

    private string BuildSummary(List<string> statuses)
    {
        var done = statuses.Count(s => s == "Completed");
        var left = statuses.Count(s => s == "Planned" || s == "InProgress");
        if (statuses.Count == 0) return "Brak wizyt na dziś";
        return $"Zakończone: {done} z {statuses.Count} · do zrobienia: {left}";
    }
}
