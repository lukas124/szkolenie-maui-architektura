using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Visits;

// Lista wizyt na dziś. Już w dobrym pierścieniu (czysty .NET, zależności przez konstruktor),
// ale wciąż robi za dużo: formatuje, buduje elementy i wybiera teksty błędów – porządkujemy w checkpoincie 03-presentation.
public partial class VisitsListViewModel(
    GetTodayVisitsHandler getTodayVisits,
    IClock clock,
    ILocalizer localizer,
    IDialogService dialogs) : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<VisitItem> Visits { get; set; } = [];

    // gest „pociągnij, aby odświeżyć” (RefreshView sam ustawia true)
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    public partial string LastUpdated { get; set; } = "";

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            var result = await getTodayVisits.HandleAsync(new GetTodayVisits(), ct);
            if (!result.IsSuccess)
            {
                await dialogs.ShowAlertAsync(localizer["Error_Title"], ErrorText(result.Error!), localizer["Common_Ok"]);
                return;
            }

            var now = clock.Now;
            var visits = result.Value;
            Visits = new ObservableCollection<VisitItem>(visits.Select(v => CreateItem(v, now)));
            Summary = BuildSummary(visits);
            LastUpdated = string.Format(localizer["Visits_LastUpdated"], now.ToLocalTime().ToString("H:mm", CultureInfo.CurrentCulture));
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private VisitItem CreateItem(VisitSummary visit, DateTimeOffset now)
    {
        var start = visit.PlannedStart.ToLocalTime();
        var end = visit.PlannedEnd.ToLocalTime();
        var day = (start.Date - now.ToLocalTime().Date).Days switch
        {
            0 => localizer["Visits_Today"],
            1 => localizer["Visits_Tomorrow"],
            _ => start.ToString("ddd dd.MM", CultureInfo.CurrentCulture)
        };
        var window = $"{day} {start:H:mm}–{end:H:mm}";
        var minutes = (int)(visit.PlannedStart - now).TotalMinutes;

        // TODO: Ćwiczenie 02 – przypisz VisitTone zamiast koloru (spóźniona → Danger, w trakcie → Info,
        // zakończona → Success, anulowana → Warning, zaplanowana → Neutral)
        var (statusKey, color) = visit.Status switch
        {
            VisitStatus.Planned when visit.IsLate => ("Visits_Status_Late", Color.FromArgb("#E5484D")),
            VisitStatus.Planned => ("Visits_Status_Planned", Color.FromArgb("#8C97A0")),
            VisitStatus.InProgress => ("Visits_Status_InProgress", Color.FromArgb("#4A90C8")),
            VisitStatus.Completed => ("Visits_Status_Completed", Color.FromArgb("#4C9E5F")),
            _ => ("Visits_Status_Cancelled", Color.FromArgb("#E09B2D"))
        };

        return new VisitItem
        {
            Id = visit.Id,
            Title = visit.CustomerName,
            Subtitle = $"{visit.DeviceName} · {visit.Address}",
            TimeText = visit.Status == VisitStatus.Planned && minutes is > 0 and < 60
                ? string.Format(localizer["Visits_StartsIn"], window, minutes)
                : window,
            StatusText = localizer[statusKey],
            StatusColor = color,
            ThumbnailUrl = visit.ThumbnailUrl,
            IsBrigade = visit.IsBrigade,
            IsCancelled = visit.Status == VisitStatus.Cancelled,
            WorkOrderNumber = visit.WorkOrderNumber,
            Address = visit.Address,
            DeviceName = visit.DeviceName,
            FaultDescription = visit.FaultDescription
        };
    }

    private string BuildSummary(IReadOnlyList<VisitSummary> visits)
    {
        if (visits.Count == 0) return localizer["Visits_Empty"];
        var done = visits.Count(v => v.Status == VisitStatus.Completed);
        var left = visits.Count(v => v.Status is VisitStatus.Planned or VisitStatus.InProgress);
        return string.Format(localizer["Visits_Summary"], done, visits.Count, left);
    }

    private string ErrorText(Error error) => error.Type switch
    {
        ErrorType.Network => localizer["Error_Network"],
        ErrorType.Unauthorized => localizer["Error_Unauthorized"],
        _ => localizer["Error_Unexpected"]
    };
}
