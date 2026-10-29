using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;

namespace FieldService.Domain.Protocols;

// Transformata: wszystkie dane na wejściu, protokół na wyjściu – liczy, nie decyduje (bez stanu i bez zależności)
public static class VisitProtocolBuilder
{
    public static VisitProtocol Build(VisitScope scope, IReadOnlyDictionary<PartId, PartInfo> parts, TimeSpan laborTime)
    {
        var lines = scope.Parts.Select(part =>
        {
            var info = parts.GetValueOrDefault(part.PartId) ?? new PartInfo(part.PartId.Value, 0m);
            var quantity = part.Quantity.Value;
            return new ProtocolPartLine(part.PartId, info.Name, quantity, info.UnitPrice, info.UnitPrice * quantity);
        }).ToList();

        return new VisitProtocol(
            scope.Activities.Select(a => a.Description).ToList(),
            lines,
            laborTime,
            lines.Sum(l => l.Total));
    }
}
