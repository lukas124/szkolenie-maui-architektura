using FieldService.Domain.Common;

namespace FieldService.Presentation.Feedback;

// Fasada: ViewModel mówi „pokaż ten błąd” albo „zapytaj o to”, a nie składa tekstów i dialogów sam
public interface IUserFeedback
{
    Task ShowErrorAsync(Error error);

    // Pytanie tak/nie; argumenty to klucze zasobów
    Task<bool> ConfirmAsync(string titleKey, string messageKey);

    // Pytanie z własnymi przyciskami (np. „Zezwól” / „Nie teraz”); wszystko to klucze zasobów
    Task<bool> ConfirmAsync(string titleKey, string messageKey, string acceptKey, string cancelKey);

    // Wybór jednej z opcji (tytuł – klucz zasobu, opcje – gotowe teksty); null = anulowano
    Task<string?> ChooseAsync(string titleKey, IReadOnlyList<string> options);
}
