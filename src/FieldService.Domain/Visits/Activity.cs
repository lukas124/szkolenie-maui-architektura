namespace FieldService.Domain.Visits;

// Obiekt wartości: wykonana czynność (kod i opis) – liczy się tylko wartość
public sealed record Activity(string Code, string Description, DateTimeOffset RecordedAt);
