using FieldService.Domain.Common;

namespace FieldService.Domain.Visits;

// Obiekt wartości: ilość sztuk. Sam pilnuje reguły „ilość > 0” – nie da się utworzyć złej ilości.
public sealed record Quantity
{
    private Quantity(int value) => Value = value;

    public int Value { get; }

    // TODO: Ćwiczenie 07 – reguła „ilość > 0”: inaczej VisitErrors.InvalidQuantity (Result, bez wyjątku)
    public static Result<Quantity> Create(int value) => Result<Quantity>.Success(new Quantity(value));

    public Quantity Add(Quantity other) => new(checked(Value + other.Value));
}
