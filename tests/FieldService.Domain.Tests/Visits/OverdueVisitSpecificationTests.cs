using FieldService.Domain.Visits;
using NUnit.Framework;

namespace FieldService.Domain.Tests.Visits;

// Specyfikacja „spóźniona wizyta”: zaplanowana, a okno już się zaczęło (Planned && plannedStart < now)
[TestFixture]
public class OverdueVisitSpecificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.FromHours(1));

    [Test]
    public void IsSatisfiedBy_PlannedAndStartBeforeNow_ReturnsTrue()
    {
        var specification = new OverdueVisitSpecification(Now);

        var result = specification.IsSatisfiedBy(VisitStatus.Planned, Now.AddMinutes(-1));

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsSatisfiedBy_PlannedAndStartAfterNow_ReturnsFalse()
    {
        var specification = new OverdueVisitSpecification(Now);

        var result = specification.IsSatisfiedBy(VisitStatus.Planned, Now.AddHours(2));

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsSatisfiedBy_PlannedAndStartEqualsNow_ReturnsFalse()
    {
        // Granica: okno zaczyna się dokładnie teraz – jeszcze nie spóźniona (ostra nierówność)
        var specification = new OverdueVisitSpecification(Now);

        var result = specification.IsSatisfiedBy(VisitStatus.Planned, Now);

        Assert.That(result, Is.False);
    }

    [TestCase(VisitStatus.InProgress)]
    [TestCase(VisitStatus.Completed)]
    [TestCase(VisitStatus.Cancelled)]
    public void IsSatisfiedBy_NotPlannedAndStartBeforeNow_ReturnsFalse(VisitStatus status)
    {
        // Rozpoczęta, zakończona albo odwołana wizyta nie jest spóźniona, nawet gdy okno minęło
        var specification = new OverdueVisitSpecification(Now);

        var result = specification.IsSatisfiedBy(status, Now.AddHours(-3));

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsSatisfiedBy_SameStartDifferentNow_DependsOnNowFromConstructor()
    {
        // „Teraz” pochodzi z konstruktora – ta sama wizyta raz jest spóźniona, raz nie
        var plannedStart = Now;
        var before = new OverdueVisitSpecification(Now.AddMinutes(-30));
        var after = new OverdueVisitSpecification(Now.AddMinutes(30));

        Assert.Multiple(() =>
        {
            Assert.That(before.IsSatisfiedBy(VisitStatus.Planned, plannedStart), Is.False);
            Assert.That(after.IsSatisfiedBy(VisitStatus.Planned, plannedStart), Is.True);
        });
    }
}
