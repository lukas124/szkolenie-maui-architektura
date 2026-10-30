using FieldService.Application.Diagnostics;
using NUnit.Framework;

namespace FieldService.Application.Tests.Diagnostics;

// Jedno źródło śladów i jeden licznik metryk aplikacji – nazwy, po których SDK (AddSource/AddMeter) je znajduje
[TestFixture]
public sealed class AppTelemetryTests
{
    [Test]
    public void Name_Always_IsFieldService()
    {
        Assert.That(AppTelemetry.Name, Is.EqualTo("FieldService"));
    }

    [Test]
    public void Source_Always_HasApplicationName()
    {
        Assert.That(AppTelemetry.Source, Is.Not.Null);
        Assert.That(AppTelemetry.Source.Name, Is.EqualTo("FieldService"));
    }

    [Test]
    public void Meter_Always_HasApplicationName()
    {
        Assert.That(AppTelemetry.Meter, Is.Not.Null);
        Assert.That(AppTelemetry.Meter.Name, Is.EqualTo("FieldService"));
    }

    [Test]
    public void VisitsCompleted_Always_IsCounterNamedVisitsCompletedInApplicationMeter()
    {
        Assert.That(AppTelemetry.VisitsCompleted, Is.Not.Null);
        Assert.That(AppTelemetry.VisitsCompleted.Name, Is.EqualTo("fieldservice.visits.completed"));
        // licznik należy do licznika metryk aplikacji (inaczej AddMeter("FieldService") go nie zbierze)
        Assert.That(AppTelemetry.VisitsCompleted.Meter, Is.SameAs(AppTelemetry.Meter));
    }
}
