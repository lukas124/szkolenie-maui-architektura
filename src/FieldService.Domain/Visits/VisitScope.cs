using FieldService.Domain.Inventory;

namespace FieldService.Domain.Visits;

// Obiekt wartości: zakres prac – to, co klient podpisuje (czynności i zużyte części). Zmiana = nowy obiekt.
public sealed record VisitScope(IReadOnlyList<Activity> Activities, IReadOnlyList<UsedPart> Parts)
{
    public static VisitScope Empty { get; } = new([], []);

    public bool IsEmpty => Activities.Count == 0 && Parts.Count == 0;

    public VisitScope WithActivity(Activity activity) => this with { Activities = [.. Activities, activity] };

    // Ta sama część drugi raz zwiększa ilość
    public VisitScope WithPart(PartId partId, Quantity quantity)
    {
        var existing = Parts.FirstOrDefault(p => p.PartId == partId);
        var parts = existing is null
            ? [.. Parts, new UsedPart(partId, quantity)]
            : Parts.Select(p => p.PartId == partId ? p with { Quantity = p.Quantity.Add(quantity) } : p).ToList();
        return this with { Parts = parts };
    }

    // Rekord porównuje listy po referencji – zakres porównujemy po zawartości (obiekt wartości)
    public bool Equals(VisitScope? other) =>
        other is not null && Activities.SequenceEqual(other.Activities) && Parts.SequenceEqual(other.Parts);

    public override int GetHashCode() => HashCode.Combine(Activities.Count, Parts.Count);
}
