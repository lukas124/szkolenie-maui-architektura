using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Queries;

// Migawka danych z innych modeli (klient i urządzenie) – tylko do wyświetlenia
public sealed record CustomerSnapshot(string Name, string Phone, string Address);

public sealed record VisitSnapshot(string WorkOrderNumber, CustomerSnapshot Customer, string DeviceName,
    string FaultDescription, string ThumbnailUrl);

// Wizyta z serwera: stan modelu + migawka
public sealed record RemoteVisit(Visit Visit, VisitSnapshot Snapshot);

public sealed record VisitPartLine(string PartId, int Quantity);

// Model odczytu szczegółów wizyty – proste dane przez granicę, nigdy agregat do ViewModelu
public sealed record VisitDetails(Guid Id, string WorkOrderNumber, VisitStatus Status, DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd, CustomerSnapshot Customer, string DeviceName, string FaultDescription, string ThumbnailUrl,
    IReadOnlyList<string> Activities, IReadOnlyList<VisitPartLine> Parts, bool HasValidSignature, string? SignerName,
    VisitOutcomeType? Outcome, int PhotoCount);
