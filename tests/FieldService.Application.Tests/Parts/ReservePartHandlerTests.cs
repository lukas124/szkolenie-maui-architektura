using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Commands;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Parts;

[TestFixture]
public class ReservePartHandlerTests
{
    private static readonly Guid Visit = Guid.Parse("6f1c2a3b-4d5e-4f60-8a7b-9c0d1e2f3a4b");

    private Mock<IInventoryGateway> _inventory = null!;
    private ReservePartHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _inventory = new Mock<IInventoryGateway>();
        _handler = new ReservePartHandler(_inventory.Object);
    }

    // Ilość < 1 → InvalidQuantity bez wywołania bramy
    [TestCase(0)]
    [TestCase(-1)]
    public async Task Handle_QuantityBelowOne_ReturnsInvalidQuantityWithoutGateway(int quantity)
    {
        var result = await _handler.HandleAsync(new ReservePart(Visit, "KOMPRESOR-R32", "MAG-01", quantity),
            CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(InventoryErrors.InvalidQuantity));
        _inventory.Verify(i => i.ReserveAsync(It.IsAny<ReservePart>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Poprawna komenda → przekazana do bramy, wynik bramy
    [Test]
    public async Task Handle_ValidCommand_ReturnsGatewayReservation()
    {
        var command = new ReservePart(Visit, "KOMPRESOR-R32", "MAG-01", 1);
        var reservation = new PartReservation(Guid.Parse("0b9e8d7c-6a5b-4c3d-9e2f-1a0b9c8d7e6f"), "KOMPRESOR-R32", "MAG-01", 1,
            new DateTimeOffset(2026, 10, 30, 14, 5, 0, TimeSpan.Zero));
        _inventory.Setup(i => i.ReserveAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PartReservation>.Success(reservation));

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(reservation));
        _inventory.Verify(i => i.ReserveAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Błąd bramy (ktoś był szybszy) → ten sam błąd
    [Test]
    public async Task Handle_GatewayOutOfStock_ReturnsSameError()
    {
        _inventory.Setup(i => i.ReserveAsync(It.IsAny<ReservePart>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PartReservation>.Failure(InventoryErrors.OutOfStock));

        var result = await _handler.HandleAsync(new ReservePart(Visit, "KOMPRESOR-R32", "MAG-01", 1), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(InventoryErrors.OutOfStock));
    }

    // Kody i typy błędów magazynu z kontraktu
    [Test]
    public void InventoryErrors_HaveContractCodes()
    {
        Assert.That(InventoryErrors.OutOfStock, Is.EqualTo(new Error("Inventory.OutOfStock", ErrorType.Conflict)));
        Assert.That(InventoryErrors.InvalidQuantity, Is.EqualTo(new Error("Inventory.InvalidQuantity", ErrorType.Validation)));
    }
}
