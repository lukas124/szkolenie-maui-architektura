namespace FieldService.Domain.Common;

// Błąd z kodem (np. "Visit.SignatureRequired") i typem. Tekst dla człowieka wybiera prezentacja na podstawie kodu.
public sealed record Error(string Code, ErrorType Type);
