using FieldService.Domain.Inventory;

namespace FieldService.Domain.Protocols;

// Dane części potrzebne do protokołu (z katalogu)
public sealed record PartInfo(string Name, decimal UnitPrice);

public sealed record ProtocolPartLine(PartId PartId, string Name, int Quantity, decimal UnitPrice, decimal Total);

// Protokół z wizyty – to, co klient widzi przed podpisem
public sealed record VisitProtocol(IReadOnlyList<string> Activities, IReadOnlyList<ProtocolPartLine> Parts,
    TimeSpan LaborTime, decimal PartsTotal);
