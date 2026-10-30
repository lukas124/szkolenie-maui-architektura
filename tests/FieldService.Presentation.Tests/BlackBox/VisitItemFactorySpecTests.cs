using FieldService.Domain.Visits;
using FieldService.Presentation.Visits;
using static FieldService.Presentation.Tests.BlackBox.SpecVisitData;
using FieldService.Application.Abstractions;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>
/// Fabryka elementu listy: ton i tekst statusu dla każdego statusu (spóźniona → Danger,
/// w trakcie → Info, zakończona → Success, anulowana → Warning, zaplanowana → Neutral),
/// teksty z formattera, „za N min” tylko dla zaplanowanej.
/// </summary>
[TestFixture]
public class VisitItemFactorySpecTests
{
    private const string WindowText = "dziś 10:00–11:30";
    private const string StartsInText = "za 15 min";

    private Mock<IVisitFormatter> _formatter = null!;
    private VisitItemFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _formatter = new Mock<IVisitFormatter>();
        _formatter.Setup(f => f.FormatTimeWindow(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>())).Returns(WindowText);
        _formatter.Setup(f => f.FormatStartsIn(It.IsAny<DateTimeOffset>())).Returns(StartsInText);
        // 07: tekst statusu z formattera (FormatStatus) – atrapa zwraca teksty jak w zasobach
        _formatter.Setup(f => f.FormatStatus(It.IsAny<VisitStatus>(), It.IsAny<bool>()))
            .Returns((VisitStatus status, bool isLate) => isLate ? SpecLocalizer.StatusLate : status switch
            {
                VisitStatus.InProgress => SpecLocalizer.StatusInProgress,
                VisitStatus.Completed => SpecLocalizer.StatusCompleted,
                VisitStatus.Cancelled => SpecLocalizer.StatusCancelled,
                _ => SpecLocalizer.StatusPlanned
            });
        _factory = new VisitItemFactory(_formatter.Object);
    }

    [TestCase(VisitStatus.Planned, VisitTone.Neutral, SpecLocalizer.StatusPlanned)]
    [TestCase(VisitStatus.InProgress, VisitTone.Info, SpecLocalizer.StatusInProgress)]
    [TestCase(VisitStatus.Completed, VisitTone.Success, SpecLocalizer.StatusCompleted)]
    [TestCase(VisitStatus.Cancelled, VisitTone.Warning, SpecLocalizer.StatusCancelled)]
    public void TonITekstStatusu_DlaKazdegoStatusu(VisitStatus status, VisitTone tone, string statusText)
    {
        var item = _factory.Create(NewVisit(At(12, 10), status));

        Assert.That(item.Tone, Is.EqualTo(tone));
        Assert.That(item.StatusText, Is.EqualTo(statusText));
        Assert.That(item.IsLate, Is.False);
    }

    [Test]
    public void Spozniona_TonDangerITekstSpozniona()
    {
        var item = _factory.Create(NewVisit(At(12, 8), isLate: true));

        Assert.That(item.Tone, Is.EqualTo(VisitTone.Danger));
        Assert.That(item.StatusText, Is.EqualTo(SpecLocalizer.StatusLate));
        Assert.That(item.IsLate, Is.True);
    }

    [TestCase(VisitStatus.Cancelled, true)]
    [TestCase(VisitStatus.Planned, false)]
    [TestCase(VisitStatus.InProgress, false)]
    [TestCase(VisitStatus.Completed, false)]
    public void IsCancelled_TylkoDlaAnulowanej(VisitStatus status, bool expected)
    {
        var item = _factory.Create(NewVisit(At(12, 10), status));

        Assert.That(item.IsCancelled, Is.EqualTo(expected));
    }

    [Test]
    public void Zaplanowana_StartsInTextZFormattera()
    {
        var visit = NewVisit(At(12, 10));

        var item = _factory.Create(visit);

        Assert.That(item.StartsInText, Is.EqualTo(StartsInText));
        _formatter.Verify(f => f.FormatStartsIn(visit.PlannedStart), Times.AtLeastOnce);
    }

    [TestCase(VisitStatus.InProgress)]
    [TestCase(VisitStatus.Completed)]
    [TestCase(VisitStatus.Cancelled)]
    public void Niezaplanowana_BezStartsInText(VisitStatus status)
    {
        var item = _factory.Create(NewVisit(At(12, 10), status));

        Assert.That(item.StartsInText, Is.Null);
    }

    [Test]
    public void OknoCzasowe_ZFormatteraDlaPoczatkuIKonca()
    {
        var visit = NewVisit(At(12, 10));

        var item = _factory.Create(visit);

        Assert.That(item.TimeWindowText, Is.EqualTo(WindowText));
        _formatter.Verify(f => f.FormatTimeWindow(visit.PlannedStart, visit.PlannedEnd), Times.AtLeastOnce);
    }

    [Test]
    public void PrzepisujeDaneWizyty()
    {
        var visit = NewVisit(At(12, 10), VisitStatus.InProgress, customer: "Anna Nowak", isBrigade: true);

        var item = _factory.Create(visit);

        Assert.That(item.Id, Is.EqualTo(visit.Id));
        Assert.That(item.CustomerName, Is.EqualTo("Anna Nowak"));
        Assert.That(item.AddressText, Is.EqualTo(visit.Address));
        Assert.That(item.DeviceName, Is.EqualTo(visit.DeviceName));
        Assert.That(item.FaultDescription, Is.EqualTo(visit.FaultDescription));
        Assert.That(item.WorkOrderNumber, Is.EqualTo(visit.WorkOrderNumber));
        Assert.That(item.IsBrigade, Is.True);
        Assert.That(item.ThumbnailUrl, Is.EqualTo(visit.ThumbnailUrl));
    }
}
