using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Tests.TestSupport;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

public class GetVisitProtocolHandlerTests
{
    private static readonly DateTimeOffset Now = TestData.Start.AddMinutes(90);

    private InMemoryVisitRepository _repository = null!;
    private FixedClock _clock = null!;
    private Mock<IInventoryGateway> _inventory = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryVisitRepository();
        _clock = new FixedClock(Now);
        _inventory = new Mock<IInventoryGateway>();
        IReadOnlyList<CatalogPart> catalog = [new("P-1", "Filtr", 25.50m), new("P-2", "Uszczelka", 4.00m)];
        _inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Ok(catalog));
    }

    private GetVisitProtocolHandler CreateHandler() => new(_repository, _inventory.Object, _clock);

    private Task UsePartAsync(VisitId id, string partId, int quantity) =>
        new UsePartHandler(_repository, _clock).HandleAsync(new UsePart(id, new PartId(partId), quantity), CancellationToken.None);

    [Test]
    public async Task HandleAsync_UnknownVisit_ReturnsNotFound()
    {
        var result = await CreateHandler().HandleAsync(new GetVisitProtocol(new VisitId(Guid.NewGuid())), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error!.Code, Is.EqualTo(VisitErrors.NotFound.Code));
    }

    [Test]
    public async Task HandleAsync_PartsInCatalog_UsesCatalogNamesAndPrices()
    {
        var id = TestData.SeedInProgress(_repository, TestData.Start);
        await UsePartAsync(id, "P-1", 2);
        await UsePartAsync(id, "P-2", 1);

        var result = await CreateHandler().HandleAsync(new GetVisitProtocol(id), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        var filter = result.Value.Parts.Single(p => p.PartId == new PartId("P-1"));
        Assert.That(filter.Name, Is.EqualTo("Filtr"));
        Assert.That(filter.Quantity, Is.EqualTo(2));
        Assert.That(filter.UnitPrice, Is.EqualTo(25.50m));
        Assert.That(filter.Total, Is.EqualTo(51.00m));
        Assert.That(result.Value.PartsTotal, Is.EqualTo(55.00m));
    }

    [Test]
    public async Task HandleAsync_RecordedActivities_AreInProtocol()
    {
        var id = TestData.SeedInProgress(_repository, TestData.Start);
        await new RecordActivityHandler(_repository, _clock)
            .HandleAsync(new RecordActivity(id, "NAPR", "Naprawa"), CancellationToken.None);

        var result = await CreateHandler().HandleAsync(new GetVisitProtocol(id), CancellationToken.None);

        // ZAŁOŻENIE: pozycja czynności w protokole zawiera jej opis
        Assert.That(result.Value.Activities.Any(a => a.Contains("Naprawa")), Is.True);
    }

    [Test]
    public async Task HandleAsync_CatalogUnavailable_UsesPartCodeAndZeroPrice()
    {
        var id = TestData.SeedInProgress(_repository, TestData.Start);
        await UsePartAsync(id, "P-1", 2);
        _inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Fail<IReadOnlyList<CatalogPart>>(TestData.NetworkError));

        var result = await CreateHandler().HandleAsync(new GetVisitProtocol(id), CancellationToken.None);

        // Brak sieci nie blokuje protokołu
        Assert.That(result.IsSuccess, Is.True);
        var line = result.Value.Parts.Single();
        Assert.That(line.Name, Is.EqualTo("P-1"));
        Assert.That(line.UnitPrice, Is.EqualTo(0m));
        Assert.That(line.Total, Is.EqualTo(0m));
        Assert.That(result.Value.PartsTotal, Is.EqualTo(0m));
    }

    [Test]
    public async Task HandleAsync_PartMissingInCatalog_UsesPartCodeAndZeroPrice()
    {
        var id = TestData.SeedInProgress(_repository, TestData.Start);
        await UsePartAsync(id, "P-9", 1);

        var result = await CreateHandler().HandleAsync(new GetVisitProtocol(id), CancellationToken.None);

        // ZAŁOŻENIE: część spoza katalogu traktujemy jak brak danych katalogu
        var line = result.Value.Parts.Single();
        Assert.That(line.Name, Is.EqualTo("P-9"));
        Assert.That(line.UnitPrice, Is.EqualTo(0m));
    }

    [Test]
    public async Task HandleAsync_StartedVisit_LaborTimeFromStartedAt()
    {
        var id = TestData.SeedInProgress(_repository, TestData.Start);

        var result = await CreateHandler().HandleAsync(new GetVisitProtocol(id), CancellationToken.None);

        Assert.That(result.Value.LaborTime, Is.EqualTo(TimeSpan.FromMinutes(90)));
    }

    [Test]
    public async Task HandleAsync_PlannedVisit_LaborTimeIsZero()
    {
        var id = TestData.SeedPlanned(_repository);

        var result = await CreateHandler().HandleAsync(new GetVisitProtocol(id), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.LaborTime, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task HandleAsync_EmptyScope_ReturnsEmptyProtocol()
    {
        var id = TestData.SeedInProgress(_repository, TestData.Start);

        var result = await CreateHandler().HandleAsync(new GetVisitProtocol(id), CancellationToken.None);

        Assert.That(result.Value.Activities, Is.Empty);
        Assert.That(result.Value.Parts, Is.Empty);
        Assert.That(result.Value.PartsTotal, Is.EqualTo(0m));
    }
}
