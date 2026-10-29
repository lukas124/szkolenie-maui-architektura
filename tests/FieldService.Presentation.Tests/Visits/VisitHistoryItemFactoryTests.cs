using System.Globalization;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

public class VisitHistoryItemFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Planned = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed = new(2026, 10, 26, 9, 10, 0, TimeSpan.Zero);

    // Atrapa lokalizatora: zwraca klucz w nawiasach – w teście widać, którego klucza użyto
    private sealed class BracketLocalizer : ILocalizer
    {
        public string this[string key] => $"[{key}]";
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("pl-PL");
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }

    private VisitFormatter _formatter = null!;
    private VisitItemFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.Now).Returns(Now);
        _formatter = new VisitFormatter(new BracketLocalizer(), clock.Object);
        _factory = new VisitItemFactory(_formatter);
    }

    private static VisitHistoryEntry Entry(VisitStatus status, VisitOutcomeType? outcome, DateTimeOffset? completedAt) =>
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "ZL-2610-1001", status, Planned, completedAt,
            "Jan Klient", "Piaseczno", "Pompa ciepła 8 kW", outcome,
            "https://api.test/api/images/abc?size=thumb", "https://api.test/api/images/abc");

    private static Func<Task> NoOp => () => Task.CompletedTask;

    [TestCase(VisitStatus.Completed, VisitOutcomeType.Fixed, VisitTone.Success)]
    [TestCase(VisitStatus.Completed, VisitOutcomeType.NeedsFollowUp, VisitTone.Warning)]
    [TestCase(VisitStatus.Completed, VisitOutcomeType.NotFixed, VisitTone.Danger)]
    [TestCase(VisitStatus.Completed, null, VisitTone.Success)]
    [TestCase(VisitStatus.Cancelled, null, VisitTone.Neutral)]
    public void CreateHistory_StatusAndOutcome_ReturnsExpectedTone(VisitStatus status, VisitOutcomeType? outcome, VisitTone expected)
    {
        var item = _factory.CreateHistory(Entry(status, outcome, Completed), NoOp);

        Assert.That(item.Tone, Is.EqualTo(expected));
    }

    [Test]
    public void CreateHistory_CompletedAtSet_VisitDateIsCompletedAt()
    {
        var item = _factory.CreateHistory(Entry(VisitStatus.Completed, VisitOutcomeType.Fixed, Completed), NoOp);

        Assert.That(item.VisitDate, Is.EqualTo(Completed));
    }

    [Test]
    public void CreateHistory_CompletedAtNull_VisitDateIsPlannedStart()
    {
        var item = _factory.CreateHistory(Entry(VisitStatus.Cancelled, null, null), NoOp);

        Assert.That(item.VisitDate, Is.EqualTo(Planned));
    }

    [Test]
    public void CreateHistory_Entry_CopiesFields()
    {
        var item = _factory.CreateHistory(Entry(VisitStatus.Completed, VisitOutcomeType.Fixed, Completed), NoOp);

        Assert.That(item.Id, Is.EqualTo(Guid.Parse("33333333-3333-3333-3333-333333333333")));
        Assert.That(item.WorkOrderNumber, Is.EqualTo("ZL-2610-1001"));
        Assert.That(item.CustomerName, Is.EqualTo("Jan Klient"));
        Assert.That(item.City, Is.EqualTo("Piaseczno"));
        Assert.That(item.DeviceName, Is.EqualTo("Pompa ciepła 8 kW"));
        Assert.That(item.ThumbnailUrl, Is.EqualTo("https://api.test/api/images/abc?size=thumb"));
        Assert.That(item.PhotoUrl, Is.EqualTo("https://api.test/api/images/abc"));
    }

    [Test]
    public void CreateHistory_Entry_StatusTextFromFormatOutcome()
    {
        var item = _factory.CreateHistory(Entry(VisitStatus.Completed, VisitOutcomeType.NotFixed, Completed), NoOp);

        Assert.That(item.StatusText, Is.EqualTo(_formatter.FormatOutcome(VisitStatus.Completed, VisitOutcomeType.NotFixed)));
    }

    [Test]
    public async Task CreateHistory_OpenCommandExecuted_CallsOpenFunction()
    {
        var calls = 0;
        var item = _factory.CreateHistory(Entry(VisitStatus.Completed, VisitOutcomeType.Fixed, Completed), () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await item.OpenCommand.ExecuteAsync(null);

        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void CreateHistory_NotExecuted_DoesNotCallOpenFunction()
    {
        var calls = 0;

        _factory.CreateHistory(Entry(VisitStatus.Completed, VisitOutcomeType.Fixed, Completed), () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.That(calls, Is.Zero);
    }

    [TestCase(VisitStatus.Completed, VisitOutcomeType.Fixed, "[Visits_Outcome_Fixed]")]
    [TestCase(VisitStatus.Completed, VisitOutcomeType.NeedsFollowUp, "[Visits_Outcome_NeedsFollowUp]")]
    [TestCase(VisitStatus.Completed, VisitOutcomeType.NotFixed, "[Visits_Outcome_NotFixed]")]
    [TestCase(VisitStatus.Completed, null, "[Visits_Status_Completed]")]
    [TestCase(VisitStatus.Cancelled, null, "[Visits_Status_Cancelled]")]
    public void FormatOutcome_StatusAndOutcome_UsesExpectedKey(VisitStatus status, VisitOutcomeType? outcome, string expected)
    {
        Assert.That(_formatter.FormatOutcome(status, outcome), Is.EqualTo(expected));
    }

    [TestCase(VisitStatus.Completed, VisitOutcomeType.Fixed, "Visits_Outcome_Fixed")]
    [TestCase(VisitStatus.Cancelled, null, "Visits_Status_Cancelled")]
    public void FormatOutcome_TestLocalizerWithoutText_ReturnsKeyOrText(VisitStatus status, VisitOutcomeType? outcome, string key)
    {
        // TestLocalizer zwraca klucz, gdy nie zna tekstu – wynik to klucz albo tekst z TestLocalizer dla tego klucza
        var localizer = new TestLocalizer();
        var clock = new Mock<IClock>();
        clock.Setup(c => c.Now).Returns(Now);
        var formatter = new VisitFormatter(localizer, clock.Object);

        Assert.That(formatter.FormatOutcome(status, outcome), Is.EqualTo(localizer[key]));
    }
}
