using FieldService.Domain.Visits;

namespace FieldService.Presentation.State;

// Stan aplikacji: dane wspólne dla ekranów i dłuższe niż ekran. Jedno źródło prawdy – ViewModele czytają stąd,
// zmieniają przez metody, a o zmianie dowiadują się z komunikatu (WeakReferenceMessenger), nie z zdarzenia singletona.
public interface IAppState
{
    VisitFilterState Filter { get; }

    // Zmienia filtr i od razu zapisuje go w telefonie (przeżyje ubicie procesu)
    void ApplyFilter(bool onlyOverdue, VisitStatus? status);
}
