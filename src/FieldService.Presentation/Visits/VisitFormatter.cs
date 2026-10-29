using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Visits;

public sealed class VisitFormatter(ILocalizer localizer, IClock clock) : IVisitFormatter
{
    public string FormatTimeWindow(DateTimeOffset from, DateTimeOffset to)
    {
        // TODO: Ćwiczenie 03 – „dziś 10:00–11:30” / „jutro 8:00–9:30” / „sob. 14.11 14:00–15:30”:
        // godziny w strefie czasowej telefonu (Local(from), Local(to)), dzień porównaj z Local(clock.Now).Date,
        // teksty „dziś”/„jutro” z localizer["Visits_Today"] / ["Visits_Tomorrow"], inny dzień: wzorzec localizer["Format_Day"],
        // godziny: wzorzec localizer["Format_Time"] – zawsze w kulturze localizer.CurrentCulture
        throw new NotImplementedException();
    }

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

    // chwila w strefie czasowej telefonu, z czasem letnim albo zimowym obowiązującym tego dnia
    private DateTimeOffset Local(DateTimeOffset moment) => TimeZoneInfo.ConvertTime(moment, localizer.TimeZone);
}
