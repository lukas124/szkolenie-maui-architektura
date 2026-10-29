using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FieldService.Presentation.Collections;

// ObservableCollection z dodawaniem paczkami: jedna strona historii = jedno powiadomienie dla CollectionView,
// a nie 30 osobnych (każde to przeliczenie listy na wątku UI)
public sealed class BatchObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        CheckReentrancy();
        var added = items.ToList();
        if (added.Count == 0) return;

        var start = Count;
        foreach (var item in added) Items.Add(item);   // Items – lista pod spodem, bez zdarzeń
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added, start));
    }
}
