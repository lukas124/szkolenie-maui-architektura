using FieldService.Application.Abstractions;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

// Ta sama chwila na telefonach w dwóch strefach czasowych i w dwóch językach. Strefę i kulturę podaje atrapa ILocalizer
// na prawdziwych zasobach (Strings.resx, Strings.en.resx), więc wynik nie zależy od ustawień maszyny z testami.
[TestFixture]
public class VisitFormatterRegionTests
{
    private const string Polish = "pl-PL";
    private const string English = "en-US";
    private const string Warsaw = "Europe/Warsaw";
    private const string NewYork = "America/New_York";

    // czwartek 12.11.2026, 12:00 UTC: w Warszawie 13:00, w Nowym Jorku 7:00
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static VisitFormatter CreateFormatter(ResxLocalizer localizer, DateTimeOffset now)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.Now).Returns(now);
        return new VisitFormatter(localizer, clock.Object);
    }

    [TestCase(Polish, Warsaw, "dziś 15:00–16:30")]
    [TestCase(Polish, NewYork, "dziś 9:00–10:30")]
    [TestCase(English, Warsaw, "today 3:00 PM–4:30 PM")]
    [TestCase(English, NewYork, "today 9:00 AM–10:30 AM")]
    public void FormatTimeWindow_Today_UsesDeviceTimeZoneAndCulture(string culture, string timeZone, string expected)
    {
        var from = new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.Zero);

        var text = CreateFormatter(new ResxLocalizer(culture, timeZone), Now).FormatTimeWindow(from, from.AddMinutes(90));

        Assert.That(text, Is.EqualTo(expected));
    }

    [TestCase(Polish, Warsaw, "sob. 14.11 14:00–15:30")]
    [TestCase(Polish, NewYork, "sob. 14.11 8:00–9:30")]
    [TestCase(English, Warsaw, "Sat 11/14 2:00 PM–3:30 PM")]
    [TestCase(English, NewYork, "Sat 11/14 8:00 AM–9:30 AM")]
    public void FormatTimeWindow_LaterDay_UsesDayPatternOfTheLanguage(string culture, string timeZone, string expected)
    {
        var from = new DateTimeOffset(2026, 11, 14, 13, 0, 0, TimeSpan.Zero);

        var text = CreateFormatter(new ResxLocalizer(culture, timeZone), Now).FormatTimeWindow(from, from.AddMinutes(90));

        Assert.That(text, Is.EqualTo(expected));
    }

    // 13.11 o 2:00 UTC to w Warszawie już piątek 3:00, a w Nowym Jorku jeszcze czwartek 21:00
    [TestCase(Polish, Warsaw, "jutro 3:00–4:00")]
    [TestCase(English, NewYork, "today 9:00 PM–10:00 PM")]
    public void FormatTimeWindow_AroundMidnight_DayDependsOnDeviceTimeZone(string culture, string timeZone, string expected)
    {
        var from = new DateTimeOffset(2026, 11, 13, 2, 0, 0, TimeSpan.Zero);

        var text = CreateFormatter(new ResxLocalizer(culture, timeZone), Now).FormatTimeWindow(from, from.AddHours(1));

        Assert.That(text, Is.EqualTo(expected));
    }

    // W Polsce czas letni zaczyna się w niedzielę 29.03.2026. „Teraz” to sobota (czas zimowy), wizyta jest w poniedziałek
    [Test]
    public void FormatTimeWindow_VisitAfterClockChangeInWarsaw_UsesOffsetOfVisitDay()
    {
        var now = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);
        var from = new DateTimeOffset(2026, 3, 30, 6, 0, 0, TimeSpan.Zero); // 8:00 czasu letniego w Warszawie

        var text = CreateFormatter(new ResxLocalizer(Polish, Warsaw), now).FormatTimeWindow(from, from.AddHours(1));

        Assert.That(text, Is.EqualTo("pon. 30.03 8:00–9:00"));
    }

    // W Nowym Jorku czas letni zaczyna się w niedzielę 8.03.2026. „Teraz” to sobota (czas zimowy), wizyta jest w poniedziałek
    [Test]
    public void FormatTimeWindow_VisitAfterClockChangeInNewYork_UsesOffsetOfVisitDay()
    {
        var now = new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.Zero);
        var from = new DateTimeOffset(2026, 3, 9, 13, 0, 0, TimeSpan.Zero); // 9:00 czasu letniego w Nowym Jorku

        var text = CreateFormatter(new ResxLocalizer(English, NewYork), now).FormatTimeWindow(from, from.AddHours(1));

        Assert.That(text, Is.EqualTo("Mon 3/9 9:00 AM–10:00 AM"));
    }

    [TestCase(Polish, Warsaw, "Aktualizacja: 13:00")]
    [TestCase(English, NewYork, "Updated: 7:00 AM")]
    public void FormatUpdatedNow_UsesDeviceTimeZoneAndCulture(string culture, string timeZone, string expected)
    {
        var text = CreateFormatter(new ResxLocalizer(culture, timeZone), Now).FormatUpdatedNow();

        Assert.That(text, Is.EqualTo(expected));
    }

    // Technik zmienia strefę czasową telefonu przy działającej aplikacji – kolejny tekst jest już w nowej strefie
    [Test]
    public void FormatTimeWindow_DeviceTimeZoneChanged_NextTextUsesNewTimeZone()
    {
        var localizer = new ResxLocalizer(Polish, Warsaw);
        var formatter = CreateFormatter(localizer, Now);
        var from = new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.Zero);

        var before = formatter.FormatTimeWindow(from, from.AddMinutes(90));
        localizer.TimeZone = TimeZoneInfo.FindSystemTimeZoneById(NewYork);
        var after = formatter.FormatTimeWindow(from, from.AddMinutes(90));

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo("dziś 15:00–16:30"));
            Assert.That(after, Is.EqualTo("dziś 9:00–10:30"));
        });
    }
}
