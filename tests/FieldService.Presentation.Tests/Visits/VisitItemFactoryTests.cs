using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

[TestFixture]
public class VisitItemFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 40, 0, TimeSpan.FromHours(1));

    private static VisitItemFactory CreateFactory()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.Now).Returns(Now);
        var localizer = new TestLocalizer();
        return new VisitItemFactory(new VisitFormatter(localizer, clock.Object));
    }

    private static VisitSummary Visit(VisitStatus status, DateTimeOffset start, bool isLate = false) =>
        new(Guid.NewGuid(), "ZL-2611-1001", status, start, start.AddMinutes(90), "Anna Wiśniewska", "ul. Lipowa 12, Piaseczno",
            "Pompa ciepła 8 kW", "Nie grzeje", IsBrigade: false, ThumbnailUrl: "https://example.com/t.png", Version: 1, IsLate: isLate);

    [TestCase(VisitStatus.InProgress, false, VisitTone.Info, "W trakcie")]
    [TestCase(VisitStatus.Completed, false, VisitTone.Success, "Zakończona")]
    [TestCase(VisitStatus.Cancelled, false, VisitTone.Warning, "Anulowana")]
    [TestCase(VisitStatus.Planned, false, VisitTone.Neutral, "Zaplanowana")]
    [TestCase(VisitStatus.Planned, true, VisitTone.Danger, "Spóźniona")]
    public void Create_MapsStatusToToneAndText(VisitStatus status, bool isLate, VisitTone tone, string text)
    {
        var item = CreateFactory().Create(Visit(status, Now.AddHours(2), isLate));

        Assert.Multiple(() =>
        {
            Assert.That(item.Tone, Is.EqualTo(tone));
            Assert.That(item.StatusText, Is.EqualTo(text));
        });
    }

    [Test]
    public void Create_PlannedVisitWithinHour_HasStartsInText()
    {
        var item = CreateFactory().Create(Visit(VisitStatus.Planned, Now.AddMinutes(20)));

        Assert.Multiple(() =>
        {
            Assert.That(item.TimeWindowText, Is.EqualTo("dziś 10:00–11:30"));
            Assert.That(item.StartsInText, Is.EqualTo("za 20 min"));
        });
    }

    [Test]
    public void Create_CancelledVisit_IsMarkedCancelled()
    {
        var item = CreateFactory().Create(Visit(VisitStatus.Cancelled, Now.AddMinutes(20)));

        Assert.Multiple(() =>
        {
            Assert.That(item.IsCancelled, Is.True);
            Assert.That(item.StartsInText, Is.Null);
        });
    }
}
