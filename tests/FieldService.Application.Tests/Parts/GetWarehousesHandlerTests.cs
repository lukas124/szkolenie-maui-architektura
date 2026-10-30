using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Parts;

[TestFixture]
public class GetWarehousesHandlerTests
{
    private Mock<IInventoryGateway> _inventory = null!;
    private GetWarehousesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _inventory = new Mock<IInventoryGateway>();
        _handler = new GetWarehousesHandler(_inventory.Object);
    }

    // Sukces bramy → te same magazyny (w tej samej kolejności)
    [Test]
    public async Task Handle_Success_ReturnsGatewayWarehouses()
    {
        IReadOnlyList<Warehouse> warehouses =
        [
            new("MAG-02", "Magazyn Południe"),
            new("MAG-01", "Magazyn Centralny"),
        ];
        _inventory.Setup(i => i.GetWarehousesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Warehouse>>.Success(warehouses));

        var result = await _handler.HandleAsync(new GetWarehouses(), CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(warehouses));
    }

    // Błąd bramy → ten sam błąd
    [Test]
    public async Task Handle_GatewayError_ReturnsSameError()
    {
        var error = new Error("Api.Unauthorized", ErrorType.Unauthorized);
        _inventory.Setup(i => i.GetWarehousesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Warehouse>>.Failure(error));

        var result = await _handler.HandleAsync(new GetWarehouses(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(error));
    }
}
