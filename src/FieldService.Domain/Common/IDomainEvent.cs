namespace FieldService.Domain.Common;

// Zdarzenie domenowe: fakt, który już się wydarzył (nazwa w czasie przeszłym)
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
