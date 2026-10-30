using FieldService.Application.Abstractions;
using FieldService.Domain.Common;

namespace FieldService.Application.Parts.Commands;

// Komenda do Magazynu (OHS): odłóż sztuki części dla wizyty – chcemy znać wynik od razu.
// Wizyta tylko jako identyfikator (Guid): moduł Parts nie zna typów modułu Visits (kierunek z mapy kontekstów).
public sealed record ReservePart(Guid VisitId, string PartId, string WarehouseId, int Quantity);

public sealed record PartReservation(Guid Id, string PartId, string WarehouseId, int Quantity, DateTimeOffset ExpiresAt);

public static class InventoryErrors
{
    // ktoś był szybszy – ostatnią sztukę zarezerwował inny technik
    public static readonly Error OutOfStock = new("Inventory.OutOfStock", ErrorType.Conflict);
    public static readonly Error InvalidQuantity = new("Inventory.InvalidQuantity", ErrorType.Validation);
}

public sealed class ReservePartHandler(IInventoryGateway inventory)
{
    public Task<Result<PartReservation>> HandleAsync(ReservePart command, CancellationToken ct) =>
        command.Quantity < 1
            ? Task.FromResult(Result<PartReservation>.Failure(InventoryErrors.InvalidQuantity))
            : inventory.ReserveAsync(command, ct);
}
