namespace FieldService.Presentation.Common;

// Żywe ViewModele (Diagnostyka, demo wycieku): każda instancja zapisana słabą referencją – rejestr nie trzyma jej przy
// życiu. „10 × szczegóły i powrót, potem pełny GC” → 1 żywy = brak wycieku, 10 = coś trzyma ViewModele.
public static class ViewModelTracker
{
    private static readonly List<WeakReference> Instances = [];
    private static readonly Lock Gate = new();

    public static void Track(object viewModel)
    {
        lock (Gate)
        {
            Instances.RemoveAll(w => !w.IsAlive);
            Instances.Add(new WeakReference(viewModel));
        }
    }

    public static int CountAlive(Type viewModelType)
    {
        lock (Gate) return Instances.Count(w => w.Target?.GetType() == viewModelType);
    }
}
