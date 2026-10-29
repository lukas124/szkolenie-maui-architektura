using FieldService.Domain.Common;

namespace FieldService.Presentation.Feedback;

// Fasada: ViewModel mówi „pokaż ten błąd” albo „zapytaj o to”, a nie składa tekstów i dialogów sam
public interface IUserFeedback
{
    Task ShowErrorAsync(Error error);

    // Pytanie tak/nie; argumenty to klucze zasobów
    Task<bool> ConfirmAsync(string titleKey, string messageKey);
}
