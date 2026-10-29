namespace FieldService.Domain.Common;

// Rodzaj błędu – po nim aplikacja decyduje, co zrobić (np. Validation → komunikat przy akcji, Conflict → komunikat
// i świeże dane)
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Network,
    Unexpected
}
