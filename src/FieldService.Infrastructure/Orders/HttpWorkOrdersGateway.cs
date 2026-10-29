using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldService.Application.Abstractions;
using FieldService.Application.Orders.Queries;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Http;

namespace FieldService.Infrastructure.Orders;

// Adapter portu IWorkOrdersGateway: „Moje zlecenia” z API (GET api/workorders)
public sealed class HttpWorkOrdersGateway(HttpClient http, AccessTokenStore tokens) : IWorkOrdersGateway
{
    private sealed record WorkOrderDto(Guid Id, string Number, string Status, string CustomerName, string Address,
        string DeviceName, DateTimeOffset SlaDueAt, int VisitCount);

    public async Task<Result<IReadOnlyList<WorkOrderSummary>>> GetMyWorkOrdersAsync(CancellationToken ct)
    {
        try
        {
            using var request = AuthorizedRequest.Create(HttpMethod.Get, "api/workorders", tokens);
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new Error("Orders.Unauthorized", ErrorType.Unauthorized);
            if (!response.IsSuccessStatusCode) return new Error("Orders.Unexpected", ErrorType.Unexpected);

            var dtos = await response.Content.ReadFromJsonAsync<List<WorkOrderDto>>(ct) ?? [];
            IReadOnlyList<WorkOrderSummary> orders = dtos.Select(d => new WorkOrderSummary(d.Id, d.Number,
                d.Status == "Closed" ? WorkOrderStatus.Closed : WorkOrderStatus.Open, d.CustomerName, d.Address,
                d.DeviceName, d.SlaDueAt, d.VisitCount)).ToList();
            return Result<IReadOnlyList<WorkOrderSummary>>.Success(orders);
        }
        catch (Exception ex) when (NetworkFailures.IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return new Error("Orders.Network", ErrorType.Network);
        }
        catch (JsonException)
        {
            // odpowiedź to nie JSON (np. Wi-Fi z portalem logowania zwraca stronę HTML)
            return new Error("Orders.Unexpected", ErrorType.Unexpected);
        }
    }
}
