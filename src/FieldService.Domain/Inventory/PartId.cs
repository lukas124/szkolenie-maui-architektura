namespace FieldService.Domain.Inventory;

// Identyfikator części z modelu Magazyn (kod części, np. "FILTR-SIATKOWY-22") – tylko identyfikator, nie cała część
public readonly record struct PartId(string Value)
{
    public override string ToString() => Value;
}
