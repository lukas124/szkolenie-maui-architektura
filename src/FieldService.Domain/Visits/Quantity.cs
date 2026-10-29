using FieldService.Domain.Common;

namespace FieldService.Domain.Visits;

// Obiekt wartości: ilość sztuk. Sam pilnuje reguły „ilość > 0” – nie da się utworzyć złej ilości.
public sealed record Quantity
{
    private Quantity(int value) => Value = value;

    public int Value { get; }

    public static Result<Quantity> Create(int value) =>
        value > 0 ? Result<Quantity>.Success(new Quantity(value)) : VisitErrors.InvalidQuantity;

    public Quantity Add(Quantity other) => new(checked(Value + other.Value));
}
