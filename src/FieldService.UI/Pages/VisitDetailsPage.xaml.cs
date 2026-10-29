using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FieldService.UI.Helpers;
using FieldService.UI.Models;
using FieldService.UI.Services;
using FieldService.UI.ViewModels;
using SQLite;

namespace FieldService.UI.Pages;

public partial class VisitDetailsPage : ContentPage
{
    private readonly Guid _id;
    private readonly ApiSession _session = ServiceHelper.GetService<ApiSession>();
    private JsonElement _visit;

    public VisitDetailsPage(Guid id)
    {
        InitializeComponent();
        _id = id;

        // od razu to, co mamy z listy; pełne dane przyjdą z API
        var cached = ServiceHelper.GetService<IVisitStore>().Find(id);
        if (cached is not null)
        {
            CustomerLabel.Text = cached.CustomerName;
            AddressLabel.Text = cached.Address;
            DeviceLabel.Text = cached.DeviceName;
            FaultLabel.Text = cached.FaultDescription;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Busy.IsRunning = true;
        try
        {
            using var http = CreateClient();
            _visit = await http.GetFromJsonAsync<JsonElement>($"api/visits/{_id}");
            Show();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Błąd", ex.Message, "OK");
        }
        finally
        {
            Busy.IsRunning = false;
        }
    }

    private void Show()
    {
        var customer = _visit.GetProperty("customer");
        var start = _visit.GetProperty("plannedStart").GetDateTimeOffset().LocalDateTime;
        var end = _visit.GetProperty("plannedEnd").GetDateTimeOffset().LocalDateTime;
        var status = _visit.GetProperty("status").GetString();

        NumberLabel.Text = "ZLECENIE " + _visit.GetProperty("workOrderNumber").GetString();
        CustomerLabel.Text = customer.GetProperty("name").GetString();
        AddressLabel.Text = $"{customer.GetProperty("street").GetString()}, {customer.GetProperty("city").GetString()} · tel. {customer.GetProperty("phone").GetString()}";
        TimeLabel.Text = $"{start:dd.MM} · {start:HH:mm}–{end:HH:mm}";
        StatusLabel.Text = status == "InProgress" ? "W trakcie" : status == "Planned" ? "Zaplanowana" : status == "Completed" ? "Zakończona" : "Anulowana";
        DeviceLabel.Text = _visit.GetProperty("deviceName").GetString();
        FaultLabel.Text = _visit.GetProperty("faultDescription").GetString();
        DeviceImage.Source = ImageSource.FromUri(new Uri(_visit.GetProperty("imageUrl").GetString()!));

        var activities = _visit.GetProperty("activities").EnumerateArray().Select(a => "• " + a.GetProperty("description").GetString());
        var parts = _visit.GetProperty("parts").EnumerateArray().Select(p => $"• {p.GetProperty("partName").GetString()} × {p.GetProperty("quantity").GetInt32()}");
        var scope = activities.Concat(parts).ToList();
        ScopeLabel.Text = scope.Count == 0 ? "Jeszcze nic" : string.Join(Environment.NewLine, scope);

        StartButton.IsVisible = status == "Planned";
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        var version = _visit.GetProperty("version").GetInt32();
        Busy.IsRunning = true;
        try
        {
            using var http = CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"api/visits/{_id}/start")
            {
                Content = JsonContent.Create(new { startedAt = DateTimeOffset.Now })
            };
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
            var response = await http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                await DisplayAlertAsync("Nie można rozpocząć", "Wizyta zmieniła się na serwerze. Odśwież listę.", "OK");
                return;
            }
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or WebException or IOException)
        {
            // bez internetu: zapisujemy w telefonie, lista wyśle przy następnym odświeżeniu
            // (Android zgłasza brak sieci także jako WebException albo IOException)
            var db = new SQLiteAsyncConnection(Path.Combine(FileSystem.AppDataDirectory, "fieldservice.db3"));
            await db.CreateTableAsync<PendingStart>();
            await db.InsertOrReplaceAsync(new PendingStart { VisitId = _id.ToString(), Version = version, StartedAt = DateTime.Now });
            await DisplayAlertAsync("Brak internetu", "Zapisano w telefonie – wyślę przy następnym odświeżeniu listy.", "OK");
        }
        finally
        {
            Busy.IsRunning = false;
        }

        VisitsViewModel.RaiseVisitsChanged();
        await Navigation.PopAsync();
    }

    private HttpClient CreateClient()
    {
        var http = new HttpClient { BaseAddress = new Uri(_session.BaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        return http;
    }
}
