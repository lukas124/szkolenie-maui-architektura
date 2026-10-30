using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Commands;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Api;

namespace FieldService.Infrastructure.Inventory;

// Adapter portu IInventoryGateway (OHS Magazynu) na wygenerowanym kliencie – wąski port zamiast całego klienta (ISP)
internal sealed class ApiInventoryGateway(FieldServiceApiClient client, ApiExecutor executor) : IInventoryGateway
{
    public async Task<Result<IReadOnlyList<VanStockItem>>> GetVanStockAsync(CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("parts.vanstock", client.GetVanStockAsync, ct);
        if (!result.IsSuccess) return result.Error!;

        IReadOnlyList<VanStockItem> items = result.Value.Select(d => new VanStockItem(d.PartId, d.PartName, d.Quantity)).ToList();
        return Result<IReadOnlyList<VanStockItem>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<CatalogPart>>> GetPartCatalogAsync(CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("parts.catalog", client.GetPartsAsync, ct);
        if (!result.IsSuccess) return result.Error!;

        IReadOnlyList<CatalogPart> parts = result.Value.Select(d => new CatalogPart(d.Id, d.Name, (decimal)d.UnitPrice)).ToList();
        return Result<IReadOnlyList<CatalogPart>>.Success(parts);
    }

    public async Task<Result<IReadOnlyList<Warehouse>>> GetWarehousesAsync(CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("parts.warehouses", client.GetWarehousesAsync, ct);
        if (!result.IsSuccess) return result.Error!;

        IReadOnlyList<Warehouse> warehouses = result.Value.Select(d => new Warehouse(d.Id, d.Name)).ToList();
        return Result<IReadOnlyList<Warehouse>>.Success(warehouses);
    }

    // Komenda do Magazynu: POST bez automatycznych ponowień (Polly) – po zgubionej odpowiedzi nie wiadomo, czy zapisano
    public async Task<Result<PartReservation>> ReserveAsync(ReservePart command, CancellationToken ct)
    {
        var request = new ReservePartRequest
        {
            PartId = command.PartId,
            WarehouseId = command.WarehouseId,
            VisitId = command.VisitId,
            Quantity = command.Quantity
        };
        var result = await executor.ExecuteAsync("parts.reserve", c => client.ReservePartAsync(request, c), ct);
        if (!result.IsSuccess) return result.Error!;

        var r = result.Value;
        return Result<PartReservation>.Success(new PartReservation(r.Id, r.PartId, r.WarehouseId, r.Quantity, r.ExpiresAt));
    }
}
