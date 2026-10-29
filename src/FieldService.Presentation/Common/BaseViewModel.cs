using CommunityToolkit.Mvvm.ComponentModel;

namespace FieldService.Presentation.Common;

// Bazowy ViewModel strony: wspólny stan (IsBusy) i haki cyklu życia. ViewModel nie wie, kiedy strona się pojawia –
// mówi mu o tym BasePage (OnAppearingAsync, OnDisappearingAsync) i usługa nawigacji (OnNavigatedToAsync).
public abstract partial class BaseViewModel : ObservableObject
{
    // true = pokaż wskaźnik ładowania (w szablonie strony)
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    // Raz, po wejściu na stronę: tu ładujemy dane. parameter = to, co przekazał nawigujący (albo null).
    public virtual Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;

    // Za każdym razem, gdy strona staje się widoczna (także po powrocie z innej strony)
    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    // Gdy strona przestaje być widoczna
    public virtual Task OnDisappearingAsync() => Task.CompletedTask;
}
