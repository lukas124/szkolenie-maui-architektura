using FieldService.Domain.Inventory;
using FieldService.Domain.Protocols;
using FieldService.Domain.Visits;

namespace FieldService.Domain.Tests.Protocols;

// Transformata: zakres wizyty + słownik części + czas pracy → protokół. Testy wejście–wyjście, bez atrap.
[TestFixture]
public class VisitProtocolBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly PartId Filter = new("P-FILTR-01");
    private static readonly PartId Valve = new("P-ZAWOR-02");

    [Test]
    public void Build_ScopeWithKnownParts_ReturnsLinesTotalsAndLaborTime()
    {
        var scope = VisitScope.Empty
            .WithActivity(new Activity("A01", "Diagnostyka", Now))
            .WithActivity(new Activity("A02", "Wymiana filtra", Now.AddMinutes(10)))
            .WithPart(Filter, Qty(2))
            .WithPart(Valve, Qty(1));
        var catalog = new Dictionary<PartId, PartInfo>
        {
            [Filter] = new("Filtr siatkowy", 25.50m),
            [Valve] = new("Zawór trójdrogowy", 310m),
        };

        var protocol = VisitProtocolBuilder.Build(scope, catalog, TimeSpan.FromMinutes(95));

        Assert.Multiple(() =>
        {
            Assert.That(protocol.Activities, Is.EqualTo(new[] { "Diagnostyka", "Wymiana filtra" }));
            Assert.That(protocol.Parts, Is.EqualTo(new[]
            {
                new ProtocolPartLine(Filter, "Filtr siatkowy", 2, 25.50m, 51.00m),
                new ProtocolPartLine(Valve, "Zawór trójdrogowy", 1, 310m, 310m),
            }));
            Assert.That(protocol.LaborTime, Is.EqualTo(TimeSpan.FromMinutes(95)));
            Assert.That(protocol.PartsTotal, Is.EqualTo(361.00m));
        });
    }

    [Test]
    public void Build_UnknownPart_UsesPartCodeAsNameAndZeroPrice()
    {
        var scope = VisitScope.Empty.WithPart(Filter, Qty(3));

        var protocol = VisitProtocolBuilder.Build(scope, new Dictionary<PartId, PartInfo>(), TimeSpan.FromMinutes(30));

        Assert.Multiple(() =>
        {
            Assert.That(protocol.Parts, Is.EqualTo(new[] { new ProtocolPartLine(Filter, "P-FILTR-01", 3, 0m, 0m) }));
            Assert.That(protocol.PartsTotal, Is.EqualTo(0m));
        });
    }

    [Test]
    public void Build_SamePartUsedTwice_GivesOneLineWithSummedQuantity()
    {
        var scope = VisitScope.Empty.WithPart(Filter, Qty(1)).WithPart(Filter, Qty(1));
        var catalog = new Dictionary<PartId, PartInfo> { [Filter] = new("Filtr siatkowy", 10m) };

        var protocol = VisitProtocolBuilder.Build(scope, catalog, TimeSpan.Zero);

        Assert.That(protocol.Parts, Is.EqualTo(new[] { new ProtocolPartLine(Filter, "Filtr siatkowy", 2, 10m, 20m) }));
    }

    [Test]
    public void Build_EmptyScope_ReturnsEmptyProtocol()
    {
        var protocol = VisitProtocolBuilder.Build(VisitScope.Empty, new Dictionary<PartId, PartInfo>(), TimeSpan.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(protocol.Activities, Is.Empty);
            Assert.That(protocol.Parts, Is.Empty);
            Assert.That(protocol.PartsTotal, Is.EqualTo(0m));
            Assert.That(protocol.LaborTime, Is.EqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public void Build_SameInput_GivesEqualOutput()
    {
        var scope = VisitScope.Empty
            .WithActivity(new Activity("A01", "Diagnostyka", Now))
            .WithPart(Filter, Qty(1));
        var catalog = new Dictionary<PartId, PartInfo> { [Filter] = new("Filtr siatkowy", 10m) };

        var first = VisitProtocolBuilder.Build(scope, catalog, TimeSpan.FromMinutes(15));
        var second = VisitProtocolBuilder.Build(scope, catalog, TimeSpan.FromMinutes(15));

        Assert.Multiple(() =>
        {
            Assert.That(second.Activities, Is.EqualTo(first.Activities));
            Assert.That(second.Parts, Is.EqualTo(first.Parts));
            Assert.That(second.PartsTotal, Is.EqualTo(first.PartsTotal));
        });
    }

    private static Quantity Qty(int value) => Quantity.Create(value).Value;
}
