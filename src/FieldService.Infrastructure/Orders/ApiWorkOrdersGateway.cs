using FieldService.Application.Abstractions;
using FieldService.Application.Orders.Queries;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Api;
using ApiStatus = FieldService.Infrastructure.Api.WorkOrderStatus;
using WorkOrderStatus = FieldService.Application.Orders.Queries.WorkOrderStatus;

namespace FieldService.Infrastructure.Orders;

// Adapter portu IWorkOrdersGateway (model Zlecenia – tylko odczyt „Moje zlecenia”)
internal sealed class ApiWorkOrdersGateway(FieldServiceApiClient client, ApiExecutor executor) : IWorkOrdersGateway
{
    public async Task<Result<IReadOnlyList<WorkOrderSummary>>> GetMyWorkOrdersAsync(CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("workorders.mine", client.GetMyWorkOrdersAsync, ct);
        if (!result.IsSuccess) return result.Error!;

        IReadOnlyList<WorkOrderSummary> orders = result.Value.Select(d => new WorkOrderSummary(d.Id, d.Number,
            d.Status == ApiStatus.Closed ? WorkOrderStatus.Closed : WorkOrderStatus.Open, d.CustomerName, d.Address,
            d.DeviceName, d.SlaDueAt, d.VisitCount)).ToList();
        return Result<IReadOnlyList<WorkOrderSummary>>.Success(orders);
    }
}
