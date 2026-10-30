using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common; // ZAŁOŻENIE: Result<T> w tej przestrzeni nazw
using FieldService.Domain.Visits;
using FieldService.TestDoubles;
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Visits;

// Protokół wizyty bierze ceny części z IPartsCatalog (zamiast z IInventoryGateway); reszta zachowania jak w 09
[TestFixture]
public sealed class GetVisitProtocolHandlerCatalogTests
{
    private InMemoryVisitRepository _visits = null!;
    private Mock<IPartsCatalog> _catalog = null!;
    private GetVisitProtocolHandler _handler = null!;
    private VisitId _visitId;

    [SetUp]
    public async Task SetUp()
    {
        _visitId = new VisitId(Guid.Parse("6f0c2a1e-8a43-4f55-9a1b-2b9a2c7d1401"));
        _visits = new InMemoryVisitRepository();
        // ZAŁOŻENIE: TestVisits.WithScope ma zużytą część TestVisits.Filter; AddAsync(Visit, CancellationToken)
        await _visits.AddAsync(TestVisits.WithScope(_visitId), CancellationToken.None);
        _catalog = new Mock<IPartsCatalog>();
        _catalog.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Success(
                [new CatalogPart("P-FILTR-01", "Filtr siatkowy", 25m)]));
        _handler = new GetVisitProtocolHandler(_visits, _catalog.Object, new FixedClock(FixedClock.DefaultNow));
    }

    // Protokół wizyty z częściami sięga po ceny do IPartsCatalog
    [Test]
    public async Task HandleAsync_VisitWithParts_UsesPartsCatalog()
    {
        // ZAŁOŻENIE: zapytanie GetVisitProtocol(VisitId) z bloku 09
        var result = await _handler.HandleAsync(new GetVisitProtocol(_visitId), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        _catalog.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
