using FieldService.Domain.Common;
using FieldService.Domain.Inventory;

namespace FieldService.Domain.Visits.Events;

// Zdarzenia wizyty – fakty w czasie przeszłym; w Bloku 11 trafiają do outboxu i na serwer
public sealed record VisitStarted(VisitId VisitId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ActivityRecorded(VisitId VisitId, Activity Activity, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PartUsed(VisitId VisitId, PartId PartId, Quantity Quantity, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PartRemoved(VisitId VisitId, PartId PartId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CustomerSigned(VisitId VisitId, string SignerName, int ScopeVersion, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CustomerSignatureInvalidated(VisitId VisitId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record VisitCompleted(VisitId VisitId, VisitOutcome Outcome, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record VisitCancelled(VisitId VisitId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
