using CommunityToolkit.Mvvm.ComponentModel;

namespace FieldService.Presentation.Common;

// Bazowy ViewModel strony: wspólny stan (IsBusy) i haki cyklu życia. ViewModel nie wie, kiedy strona się pojawia –
// mówi mu o tym BasePage (OnAppearingAsync, OnDisappearingAsync, OnClosed) i usługa nawigacji (INavigatedTo<TRoute>).
public abstract partial class BaseViewModel : ObservableObject
{
    // każdy ViewModel w rejestrze żywych instancji (słaba referencja – Diagnostyka)
    protected BaseViewModel() => ViewModelTracker.Track(this);

    // true = pokaż wskaźnik ładowania (w szablonie strony)
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    // Za każdym razem, gdy strona staje się widoczna (także po powrocie z innej strony)
    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    // Gdy strona przestaje być widoczna
    public virtual Task OnDisappearingAsync() => Task.CompletedTask;

    // Strona zdjęta ze stosu (wstecz, gest iOS, zamknięcie modala, reset stosu) – sprzątanie, np. odpięcie subskrypcji
    public virtual void OnClosed()
    {
    }
}
