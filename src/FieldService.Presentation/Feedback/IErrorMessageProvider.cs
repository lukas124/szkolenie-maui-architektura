using FieldService.Domain.Common;

namespace FieldService.Presentation.Feedback;

// Błąd (kod) → tekst dla użytkownika
public interface IErrorMessageProvider
{
    string GetMessage(Error error);
}
