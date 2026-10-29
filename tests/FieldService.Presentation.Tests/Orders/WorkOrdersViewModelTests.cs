using FieldService.Application.Abstractions;
using FieldService.Application.Orders.Queries;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Orders;
using FieldService.Presentation.Tests.Contract;

namespace FieldService.Presentation.Tests.Orders;

// Ekran zleceń: po wejściu lista z przypadku użycia; błąd → ShowErrorAsync
[TestFixture]
public class WorkOrdersViewModelTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 29, 0, 0, 0, TimeSpan.FromHours(1));

    private Mock<IWorkOrdersGateway> _gateway = null!;
    private Mock<IUserFeedback> _feedback = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IWorkOrdersGateway>();
        _feedback = new Mock<IUserFeedback>();
    }

    private WorkOrdersViewModel CreateViewModel() =>
        new(new GetMyWorkOrdersHandler(_gateway.Object), _feedback.Object);

    [Test]
    public async Task OnNavigatedToAsync_GatewayReturnsOrders_FillsWorkOrdersInUseCaseOrder()
    {
        _gateway.Setup(g => g.GetMyWorkOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Ok<IReadOnlyList<WorkOrderSummary>>(new[]
            {
                ContractTestData.WorkOrder("Z-closed", WorkOrderStatus.Closed, Day.AddHours(1)),
                ContractTestData.WorkOrder("Z-open-late", WorkOrderStatus.Open, Day.AddHours(20)),
                ContractTestData.WorkOrder("Z-open-early", WorkOrderStatus.Open, Day.AddHours(4)),
            }));
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(new WorkOrdersRoute());

        Assert.That(viewModel.WorkOrders.Select(o => o.Number),
            Is.EqualTo(new[] { "Z-open-early", "Z-open-late", "Z-closed" }));
    }

    [Test]
    public async Task OnNavigatedToAsync_GatewayReturnsOrders_DoesNotShowError()
    {
        _gateway.Setup(g => g.GetMyWorkOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Ok<IReadOnlyList<WorkOrderSummary>>(Array.Empty<WorkOrderSummary>()));
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(new WorkOrdersRoute());

        _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<FieldService.Domain.Common.Error>()), Times.Never);
    }

    [Test]
    public async Task OnNavigatedToAsync_GatewayError_ShowsErrorAndLeavesListEmpty()
    {
        var error = ContractTestData.NetworkError();
        _gateway.Setup(g => g.GetMyWorkOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Fail<IReadOnlyList<WorkOrderSummary>>(error));
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(new WorkOrdersRoute());

        _feedback.Verify(f => f.ShowErrorAsync(error), Times.Once);
        Assert.That(viewModel.WorkOrders, Is.Empty);
    }

    [Test]
    public async Task OnNavigatedToAsync_EnteredTwice_ListNotDuplicated()
    {
        _gateway.Setup(g => g.GetMyWorkOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Ok<IReadOnlyList<WorkOrderSummary>>(new[]
            {
                ContractTestData.WorkOrder("Z1", WorkOrderStatus.Open, Day.AddHours(2)),
            }));
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(new WorkOrdersRoute());
        await viewModel.OnNavigatedToAsync(new WorkOrdersRoute());

        Assert.That(viewModel.WorkOrders, Has.Count.EqualTo(1));
    }
}
