using FieldService.Application.Abstractions;
using FieldService.Application.Orders.Queries;
using FieldService.Application.Tests.Contract;

namespace FieldService.Application.Tests.Orders;

// Zlecenia technika: najpierw otwarte, potem po terminie SLA rosnąco; błąd bramy → ten sam błąd
[TestFixture]
public class GetMyWorkOrdersHandlerTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 29, 0, 0, 0, TimeSpan.FromHours(1));

    private Mock<IWorkOrdersGateway> _gateway = null!;

    [SetUp]
    public void SetUp() => _gateway = new Mock<IWorkOrdersGateway>();

    private void GatewayReturns(params WorkOrderSummary[] orders) =>
        _gateway.Setup(g => g.GetMyWorkOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Ok<IReadOnlyList<WorkOrderSummary>>(orders));

    private async Task<string[]> HandleAndGetNumbersAsync()
    {
        var result = await new GetMyWorkOrdersHandler(_gateway.Object).HandleAsync(new GetMyWorkOrders(), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        return result.Value.Select(o => o.Number).ToArray();
    }

    [Test]
    public async Task HandleAsync_OpenAndClosed_ReturnsOpenFirst()
    {
        GatewayReturns(
            ContractTestData.WorkOrder("Z1", WorkOrderStatus.Closed, Day.AddHours(1)),
            ContractTestData.WorkOrder("Z2", WorkOrderStatus.Open, Day.AddHours(20)));

        var numbers = await HandleAndGetNumbersAsync();

        Assert.That(numbers, Is.EqualTo(new[] { "Z2", "Z1" }));
    }

    [Test]
    public async Task HandleAsync_SameStatus_SortsBySlaAscending()
    {
        GatewayReturns(
            ContractTestData.WorkOrder("Z1", WorkOrderStatus.Open, Day.AddHours(30)),
            ContractTestData.WorkOrder("Z2", WorkOrderStatus.Open, Day.AddHours(5)),
            ContractTestData.WorkOrder("Z3", WorkOrderStatus.Open, Day.AddHours(12)));

        var numbers = await HandleAndGetNumbersAsync();

        Assert.That(numbers, Is.EqualTo(new[] { "Z2", "Z3", "Z1" }));
    }

    [Test]
    public async Task HandleAsync_MixedOrders_OpenBySlaThenClosedBySla()
    {
        GatewayReturns(
            ContractTestData.WorkOrder("C-late", WorkOrderStatus.Closed, Day.AddHours(40)),
            ContractTestData.WorkOrder("O-late", WorkOrderStatus.Open, Day.AddHours(30)),
            ContractTestData.WorkOrder("C-early", WorkOrderStatus.Closed, Day.AddHours(2)),
            ContractTestData.WorkOrder("O-early", WorkOrderStatus.Open, Day.AddHours(8)));

        var numbers = await HandleAndGetNumbersAsync();

        Assert.That(numbers, Is.EqualTo(new[] { "O-early", "O-late", "C-early", "C-late" }));
    }

    [Test]
    public async Task HandleAsync_NoOrders_ReturnsEmptyList()
    {
        GatewayReturns();

        var numbers = await HandleAndGetNumbersAsync();

        Assert.That(numbers, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_GatewayError_ReturnsSameError()
    {
        var error = ContractTestData.NetworkError();
        _gateway.Setup(g => g.GetMyWorkOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Fail<IReadOnlyList<WorkOrderSummary>>(error));

        var result = await new GetMyWorkOrdersHandler(_gateway.Object).HandleAsync(new GetMyWorkOrders(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(error));
        });
    }
}
