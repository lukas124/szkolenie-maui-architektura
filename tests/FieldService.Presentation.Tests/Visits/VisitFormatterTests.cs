using FieldService.Application.Abstractions;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

// Formatter testujemy bez emulatora: „teraz” podaje atrapa zegara, kulturę – atrapa lokalizatora
[TestFixture]
[Ignore("Ćwiczenie 03: usuń Ignore po uzupełnieniu TODO")]
public class VisitFormatterTests
{
    private static readonly TimeSpan Warsaw = TimeSpan.FromHours(1);
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 40, 0, Warsaw); // czwartek

    private static VisitFormatter CreateFormatter()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.Now).Returns(Now);
        return new VisitFormatter(new TestLocalizer(), clock.Object);
    }

    [Test]
    public void FormatTimeWindow_Today_StartsWithToday()
    {
        var text = CreateFormatter().FormatTimeWindow(Now.AddMinutes(20), Now.AddMinutes(110));

        Assert.That(text, Is.EqualTo("dziś 10:00–11:30"));
    }

    [Test]
    public void FormatTimeWindow_Tomorrow_StartsWithTomorrow()
    {
        var from = new DateTimeOffset(2026, 11, 13, 8, 0, 0, Warsaw);

        var text = CreateFormatter().FormatTimeWindow(from, from.AddMinutes(90));

        Assert.That(text, Is.EqualTo("jutro 8:00–9:30"));
    }

    [Test]
    public void FormatTimeWindow_LaterDay_ShowsShortDayNameAndDate()
    {
        var from = new DateTimeOffset(2026, 11, 14, 14, 0, 0, Warsaw);

        var text = CreateFormatter().FormatTimeWindow(from, from.AddMinutes(90));

        Assert.That(text, Is.EqualTo("sob. 14.11 14:00–15:30"));
    }

    [Test]
    public void FormatTimeWindow_UsesTechnicianTimeZone_NotUtc()
    {
        var fromUtc = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero); // 10:00 w Warszawie

        var text = CreateFormatter().FormatTimeWindow(fromUtc, fromUtc.AddHours(1));

        Assert.That(text, Is.EqualTo("dziś 10:00–11:00"));
    }

    [TestCase(20, "za 20 min")]
    [TestCase(59, "za 59 min")]
    [TestCase(90, null)]
    [TestCase(-5, null)]
    public void FormatStartsIn_OnlyWithinTheNextHour(int minutes, string? expected)
    {
        Assert.That(CreateFormatter().FormatStartsIn(Now.AddMinutes(minutes)), Is.EqualTo(expected));
    }
}
