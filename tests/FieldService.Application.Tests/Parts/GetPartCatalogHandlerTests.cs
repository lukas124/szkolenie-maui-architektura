using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Parts;

[TestFixture]
public class GetPartCatalogHandlerTests
{
    private Mock<IInventoryGateway> _inventory = null!;
    private GetPartCatalogHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _inventory = new Mock<IInventoryGateway>();
        _handler = new GetPartCatalogHandler(_inventory.Object);
    }

    // Katalog posortowany po nazwie
    [Test]
    public async Task Handle_Success_ReturnsPartsSortedByName()
    {
        IReadOnlyList<CatalogPart> parts =
        [
            new("ZAWOR-3D", "Zawór trójdrogowy", 310m),
            new("FILTR-SIATKOWY-22", "Filtr siatkowy 22 mm", 49.99m),
            new("KOMPRESOR-R32", "Kompresor R32", 1899.5m),
        ];
        _inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Success(parts));

        var result = await _handler.HandleAsync(new GetPartCatalog(), CancellationToken.None);

        Assert.That(result.Value.Select(p => p.Name),
            Is.EqualTo(new[] { "Filtr siatkowy 22 mm", "Kompresor R32", "Zawór trójdrogowy" }));
    }

    // Pusty katalog → pusta lista
    [Test]
    public async Task Handle_EmptyCatalog_ReturnsEmptyList()
    {
        _inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Success(Array.Empty<CatalogPart>()));

        var result = await _handler.HandleAsync(new GetPartCatalog(), CancellationToken.None);

        Assert.That(result.Value, Is.Empty);
    }

    // Błąd bramy → ten sam błąd
    [Test]
    public async Task Handle_GatewayError_ReturnsSameError()
    {
        var error = new Error("Api.Network", ErrorType.Network);
        _inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Failure(error));

        var result = await _handler.HandleAsync(new GetPartCatalog(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(error));
    }
}
