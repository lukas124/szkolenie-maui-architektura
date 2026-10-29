using System.Collections.ObjectModel;

namespace FieldService.Presentation.Common;

public static class ObservableCollectionExtensions
{
    // Podmienia zawartość kolekcji związanej z widokiem (ta sama instancja – binding zostaje)
    public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items) collection.Add(item);
    }
}
