namespace FieldService.Domain.Common;

// Korzeń agregatu: tożsamość, wersja do blokowania optymistycznego i lista zdarzeń zebranych od ostatniego zapisu
public abstract class AggregateRoot<TId>(TId id, int version)
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; } = id;

    // Wersja zapisanego stanu – zapis z inną wersją niż w bazie to konflikt (ktoś zapisał wcześniej)
    public int Version { get; private set; } = version;

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    // Tylko repozytorium, po udanym zapisie: zdarzenia obsłużone, zapisany stan ma nową wersję
    public void ClearDomainEvents() => _domainEvents.Clear();

    public void IncrementVersion() => Version++;
}
