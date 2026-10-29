using FieldService.Application.Orders.Queries;
using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Port: brama do modelu Zlecenia (GET api/workorders)
public interface IWorkOrdersGateway
{
    Task<Result<IReadOnlyList<WorkOrderSummary>>> GetMyWorkOrdersAsync(CancellationToken ct);
}
