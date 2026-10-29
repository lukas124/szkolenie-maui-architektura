using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Orders.Queries;

public enum WorkOrderStatus
{
    Open,
    Closed
}

// Model odczytu modelu Zlecenia (serwer jest źródłem prawdy; w aplikacji tylko do wyświetlenia – Conformist)
public sealed record WorkOrderSummary(Guid Id, string Number, WorkOrderStatus Status, string CustomerName, string Address,
    string DeviceName, DateTimeOffset SlaDueAt, int VisitCount);

// Zapytanie: „Moje zlecenia” – najpierw otwarte, potem po terminie SLA
public sealed record GetMyWorkOrders;

public sealed class GetMyWorkOrdersHandler(IWorkOrdersGateway gateway)
{
    public async Task<Result<IReadOnlyList<WorkOrderSummary>>> HandleAsync(GetMyWorkOrders query, CancellationToken ct)
    {
        var result = await gateway.GetMyWorkOrdersAsync(ct);
        if (!result.IsSuccess) return result;

        IReadOnlyList<WorkOrderSummary> ordered = result.Value
            .OrderBy(o => o.Status == WorkOrderStatus.Open ? 0 : 1)
            .ThenBy(o => o.SlaDueAt)
            .ToList();
        return Result<IReadOnlyList<WorkOrderSummary>>.Success(ordered);
    }
}
