using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Visits;

public sealed class VisitFormatter(ILocalizer localizer, IClock clock) : IVisitFormatter
{
    public string FormatTimeWindow(DateTimeOffset from, DateTimeOffset to)
    {
        // dzień i godziny liczymy w strefie czasowej telefonu, wzorce bierzemy z zasobów, a nazwy dni z kultury
        var start = Local(from);
        var end = Local(to);
        var culture = localizer.CurrentCulture;
        var time = localizer["Format_Time"];

        var day = (start.Date - Local(clock.Now).Date).Days switch
        {
            0 => localizer["Visits_Today"],
            1 => localizer["Visits_Tomorrow"],
            _ => start.ToString(localizer["Format_Day"], culture)
        };
        return $"{day} {start.ToString(time, culture)}–{end.ToString(time, culture)}";
    }

    public string FormatStatus(VisitStatus status, bool isLate) => localizer[status switch
    {
        VisitStatus.Planned when isLate => "Visits_Status_Late",
        VisitStatus.Planned => "Visits_Status_Planned",
        VisitStatus.InProgress => "Visits_Status_InProgress",
        VisitStatus.Completed => "Visits_Status_Completed",
        _ => "Visits_Status_Cancelled"
    }];

    public string? FormatStartsIn(DateTimeOffset from)
    {
        var minutes = (int)Math.Ceiling((from - clock.Now).TotalMinutes);
        return minutes is > 0 and < 60 ? string.Format(localizer["Visits_StartsIn"], minutes) : null;
    }

    public string FormatDaySummary(IReadOnlyList<VisitSummary> visits)
    {
        if (visits.Count == 0) return localizer["Visits_Empty"];
        var done = visits.Count(v => v.Status == VisitStatus.Completed);
        var left = visits.Count(v => v.Status is VisitStatus.Planned or VisitStatus.InProgress);
        return string.Format(localizer["Visits_Summary"], done, visits.Count, left);
    }

    public string FormatUpdatedNow() =>
        string.Format(localizer["Visits_LastUpdated"],
            Local(clock.Now).ToString(localizer["Format_Time"], localizer.CurrentCulture));

    public string FormatOfflineSince(DateTimeOffset savedAt)
    {
        var local = Local(savedAt);
        var format = localizer[local.Date == Local(clock.Now).Date ? "Format_Time" : "Format_DayTime"];
        return string.Format(localizer["Visits_OfflineSince"], local.ToString(format, localizer.CurrentCulture));
    }

    public string FormatPending(int count) => string.Format(localizer.CurrentCulture, localizer["Visits_PendingSync"], count);

    // chwila w strefie czasowej telefonu, z czasem letnim albo zimowym obowiązującym tego dnia
    private DateTimeOffset Local(DateTimeOffset moment) => TimeZoneInfo.ConvertTime(moment, localizer.TimeZone);
}
