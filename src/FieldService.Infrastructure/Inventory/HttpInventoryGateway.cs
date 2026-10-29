using System.Net.Http.Json;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Http;

namespace FieldService.Infrastructure.Inventory;

// Adapter portu IInventoryGateway: auto technika z API (GET api/vanstock)
public sealed class HttpInventoryGateway(HttpClient http, AccessTokenStore tokens) : IInventoryGateway
{
    private sealed record VanStockDto(string PartId, string PartName, int Quantity);

    public async Task<Result<IReadOnlyList<VanStockItem>>> GetVanStockAsync(CancellationToken ct)
    {
        try
        {
            using var request = AuthorizedRequest.Create(HttpMethod.Get, "api/vanstock", tokens);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return new Error("Inventory.Unexpected", ErrorType.Unexpected);

            var dtos = await response.Content.ReadFromJsonAsync<List<VanStockDto>>(ct) ?? [];
            IReadOnlyList<VanStockItem> items = dtos.Select(d => new VanStockItem(d.PartId, d.PartName, d.Quantity)).ToList();
            return Result<IReadOnlyList<VanStockItem>>.Success(items);
        }
        catch (Exception ex) when (NetworkFailures.IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return new Error("Inventory.Network", ErrorType.Network);
        }
    }
}
