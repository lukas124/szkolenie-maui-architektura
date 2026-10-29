using System.Net.Http.Json;
using System.Text.Json;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Http;

namespace FieldService.Infrastructure.Inventory;

// Adapter portu IInventoryGateway: auto technika (GET api/vanstock) i katalog części (GET api/parts) z API
public sealed class HttpInventoryGateway(HttpClient http, AccessTokenStore tokens) : IInventoryGateway
{
    private sealed record VanStockDto(string PartId, string PartName, int Quantity);

    private sealed record PartDto(string Id, string Name, decimal UnitPrice);

    public Task<Result<IReadOnlyList<VanStockItem>>> GetVanStockAsync(CancellationToken ct) =>
        GetListAsync<VanStockDto, VanStockItem>("api/vanstock", d => new VanStockItem(d.PartId, d.PartName, d.Quantity), ct);

    public Task<Result<IReadOnlyList<CatalogPart>>> GetPartCatalogAsync(CancellationToken ct) =>
        GetListAsync<PartDto, CatalogPart>("api/parts", d => new CatalogPart(d.Id, d.Name, d.UnitPrice), ct);

    private async Task<Result<IReadOnlyList<TItem>>> GetListAsync<TDto, TItem>(string url, Func<TDto, TItem> map, CancellationToken ct)
    {
        try
        {
            using var request = AuthorizedRequest.Create(HttpMethod.Get, url, tokens);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return new Error("Inventory.Unexpected", ErrorType.Unexpected);

            var dtos = await response.Content.ReadFromJsonAsync<List<TDto>>(ct) ?? [];
            IReadOnlyList<TItem> items = dtos.Select(map).ToList();
            return Result<IReadOnlyList<TItem>>.Success(items);
        }
        catch (Exception ex) when (NetworkFailures.IsNetwork(ex) && !ct.IsCancellationRequested)
        {
            return new Error("Inventory.Network", ErrorType.Network);
        }
        catch (JsonException)
        {
            // odpowiedź to nie JSON (np. Wi-Fi z portalem logowania zwraca stronę HTML)
            return new Error("Inventory.Unexpected", ErrorType.Unexpected);
        }
    }
}
