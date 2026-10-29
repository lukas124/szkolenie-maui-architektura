using System.Collections.Specialized;
using FieldService.Presentation.Collections;

namespace FieldService.Presentation.Tests.Collections;

public class BatchObservableCollectionTests
{
    private BatchObservableCollection<string> _collection = null!;
    private List<NotifyCollectionChangedEventArgs> _events = null!;

    [SetUp]
    public void SetUp()
    {
        _collection = new BatchObservableCollection<string>();
        _events = [];
        _collection.CollectionChanged += (_, e) => _events.Add(e);
    }

    [Test]
    public void AddRange_ThreeItems_RaisesSingleAddEvent()
    {
        _collection.AddRange(["a", "b", "c"]);

        Assert.That(_events, Has.Count.EqualTo(1));
        Assert.That(_events[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
    }

    [Test]
    public void AddRange_ThreeItems_NewItemsContainWholeBatch()
    {
        _collection.AddRange(["a", "b", "c"]);

        Assert.That(_events[0].NewItems, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void AddRange_EmptyCollection_NewStartingIndexIsZero()
    {
        _collection.AddRange(["a", "b"]);

        Assert.That(_events[0].NewStartingIndex, Is.EqualTo(0));
    }

    [Test]
    public void AddRange_CollectionWithItems_NewStartingIndexIsPreviousCount()
    {
        _collection.AddRange(["a", "b"]);
        _events.Clear();

        _collection.AddRange(["c", "d", "e"]);

        Assert.That(_events, Has.Count.EqualTo(1));
        Assert.That(_events[0].NewStartingIndex, Is.EqualTo(2));
    }

    [Test]
    public void AddRange_EmptyBatch_RaisesNoEvent()
    {
        _collection.Add("a");
        _events.Clear();

        _collection.AddRange([]);

        Assert.That(_events, Is.Empty);
        Assert.That(_collection, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void AddRange_TwoBatches_ContainsItemsInOrder()
    {
        _collection.Add("a");

        _collection.AddRange(["b", "c"]);
        _collection.AddRange(["d"]);

        Assert.That(_collection, Is.EqualTo(new[] { "a", "b", "c", "d" }));
    }

    [Test]
    public void AddRange_LazySequence_AddsAllItems()
    {
        // Paczka jako leniwa sekwencja – też jedno zdarzenie i komplet elementów
        _collection.AddRange(Enumerable.Range(1, 3).Select(i => $"e{i}"));

        Assert.That(_events, Has.Count.EqualTo(1));
        Assert.That(_collection, Is.EqualTo(new[] { "e1", "e2", "e3" }));
    }
}
