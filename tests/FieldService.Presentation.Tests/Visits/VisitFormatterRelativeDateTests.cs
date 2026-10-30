using System.Globalization;
using FieldService.Presentation.Common;
using FieldService.Presentation.Visits;
using FieldService.TestDoubles;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Visits;

// IVisitFormatter.FormatRelativeDate i FormatSnapshot; lokalizator-atrapa: klucz → tekst
[TestFixture]
public sealed class VisitFormatterRelativeDateTests
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["Time_Today"] = "dziś",
        ["Time_Yesterday"] = "wczoraj",
        ["Time_DaysAgo"] = "{0} dni temu",
        ["Visits_SnapshotFrom"] = "Dane z {0}",
        ["Format_Time"] = "H:mm",
        ["Format_DayTime"] = "d.MM H:mm",
        ["Format_Date"] = "d.MM.yyyy",
    };

    // strefa czasowa telefonu w teście – wynik nie zależy od strefy maszyny, na której działają testy
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    private VisitFormatter _formatter = null!;

    // Dzień października 2026 w czasie lokalnym technika (Europe/Warsaw). „Dziś” = 28.10.2026 12:00
    private static DateTimeOffset Local(int day, int hour, int minute = 0)
    {
        var local = new DateTime(2026, 10, day, hour, minute, 0);
        return new DateTimeOffset(local, Warsaw.GetUtcOffset(local));
    }

    [SetUp]
    public void SetUp()
    {
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => Texts.TryGetValue(key, out var text) ? text : key);
        localizer.Setup(l => l.CurrentCulture).Returns(CultureInfo.GetCultureInfo("pl-PL"));
        localizer.Setup(l => l.TimeZone).Returns(Warsaw);
        // ZAŁOŻENIE: konstruktor VisitFormatter(ILocalizer localizer, IClock clock); FixedClock(DateTimeOffset now)
        _formatter = new VisitFormatter(localizer.Object, new FixedClock(Local(28, 12)));
    }

    // Ten sam dzień → „dziś”
    [Test]
    public void FormatRelativeDate_SameDay_ReturnsToday()
    {
        Assert.That(_formatter.FormatRelativeDate(Local(28, 8)), Is.EqualTo("dziś"));
    }

    // Poprzedni dzień (także późnym wieczorem) → „wczoraj”
    [TestCase(9)]
    [TestCase(23)]
    public void FormatRelativeDate_PreviousDay_ReturnsYesterday(int hour)
    {
        Assert.That(_formatter.FormatRelativeDate(Local(27, hour)), Is.EqualTo("wczoraj"));
    }

    // 2–6 dni temu → „{n} dni temu”
    [TestCase(26, "2 dni temu")]
    [TestCase(25, "3 dni temu")]
    [TestCase(22, "6 dni temu")]
    public void FormatRelativeDate_TwoToSixDaysAgo_ReturnsDaysAgo(int day, string expected)
    {
        Assert.That(_formatter.FormatRelativeDate(Local(day, 10)), Is.EqualTo(expected));
    }

    // 7 dni i więcej → data „d.MM.yyyy”
    [TestCase(21, "21.10.2026")]
    [TestCase(1, "1.10.2026")]
    public void FormatRelativeDate_SevenOrMoreDaysAgo_ReturnsDate(int day, string expected)
    {
        Assert.That(_formatter.FormatRelativeDate(Local(day, 10)), Is.EqualTo(expected));
    }

    // Granica dnia liczona w czasie lokalnym, nie po różnicy 24 h: wczoraj 23:59 to „wczoraj”, dziś 0:01 to „dziś”
    [Test]
    public void FormatRelativeDate_AroundMidnight_UsesCalendarDays()
    {
        Assert.That(_formatter.FormatRelativeDate(Local(27, 23, 59)), Is.EqualTo("wczoraj"));
        Assert.That(_formatter.FormatRelativeDate(Local(28, 0, 1)), Is.EqualTo("dziś"));
    }

    // Migawka z dziś → „Dane z 7:42” (godzina H:mm)
    [Test]
    public void FormatSnapshot_SavedToday_ReturnsHourOnly()
    {
        Assert.That(_formatter.FormatSnapshot(Local(28, 7, 42)), Is.EqualTo("Dane z 7:42"));
    }

    // Migawka z innego dnia → „Dane z …” z godziną i dniem (dzień jak w FormatOfflineSince)
    [Test]
    public void FormatSnapshot_SavedOnOtherDay_ContainsHourAndMore()
    {
        var text = _formatter.FormatSnapshot(Local(26, 7, 42));

        Assert.That(text, Does.StartWith("Dane z "));
        Assert.That(text, Does.Contain("7:42"));
        Assert.That(text, Is.Not.EqualTo("Dane z 7:42"));
    }
}
