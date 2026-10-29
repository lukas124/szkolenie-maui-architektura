using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Domain.Tests.Visits;

// Obiekt wartości Quantity: walidacja „ilość > 0” w metodzie wytwórczej i porównanie po wartości.
[TestFixture]
public class QuantityTests
{
    // Karta Bloku 07: poprawna ilość.
    [Test]
    public void Create_PositiveValue_ReturnsQuantity()
    {
        var result = Quantity.Create(3);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(3));
        });
    }

    // Karta Bloku 07: zerowa ilość – błąd walidacji, bez wyjątku.
    [Test]
    public void Create_Zero_FailsWithInvalidQuantity()
    {
        var result = Quantity.Create(0);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(VisitErrors.InvalidQuantity));
            Assert.That(result.Error?.Type, Is.EqualTo(ErrorType.Validation));
        });
    }

    [Test]
    public void Create_Negative_FailsWithInvalidQuantity()
    {
        var result = Quantity.Create(-1);

        Assert.That(result.Error, Is.EqualTo(VisitErrors.InvalidQuantity));
    }

    [Test]
    public void Value_OfFailedResult_Throws()
    {
        var result = Quantity.Create(0);

        Assert.That(() => result.Value, Throws.InvalidOperationException);
    }

    [Test]
    public void Quantities_WithSameValue_AreEqual()
    {
        var first = Quantity.Create(2).Value;
        var second = Quantity.Create(2).Value;

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(ReferenceEquals(first, second), Is.False);
        });
    }

    [Test]
    public void Quantities_WithDifferentValue_AreNotEqual()
    {
        Assert.That(Quantity.Create(2).Value, Is.Not.EqualTo(Quantity.Create(3).Value));
    }

    [Test]
    public void Add_ReturnsSumAndKeepsOperandsUnchanged()
    {
        var two = Quantity.Create(2).Value;
        var three = Quantity.Create(3).Value;

        var sum = two.Add(three);

        Assert.Multiple(() =>
        {
            Assert.That(sum, Is.EqualTo(Quantity.Create(5).Value));
            Assert.That(two.Value, Is.EqualTo(2));
            Assert.That(three.Value, Is.EqualTo(3));
        });
    }
}
