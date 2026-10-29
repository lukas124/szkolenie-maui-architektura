using FieldService.Domain.Inventory;

namespace FieldService.Domain.Visits;

// Obiekt wartości: część zamontowana u klienta (identyfikator z modelu Magazyn + ilość)
public sealed record UsedPart(PartId PartId, Quantity Quantity);
