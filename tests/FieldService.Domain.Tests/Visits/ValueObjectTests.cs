using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;

namespace FieldService.Domain.Tests.Visits;

// Obiekty wartości wizyty: CustomerSignature, VisitOutcome, VisitScope.
[TestFixture]
public class ValueObjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly PartId Filter = new("P-FILTR-01");
    private static readonly PartId Valve = new("P-ZAWOR-02");

    // ---------- CustomerSignature ----------

    [Test]
    public void CustomerSignature_Create_KeepsSignerTimeAndScopeVersion()
    {
        var result = CustomerSignature.Create("Jan Klient", Now, 4);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.SignerName, Is.EqualTo("Jan Klient"));
            Assert.That(result.Value.SignedAt, Is.EqualTo(Now));
            Assert.That(result.Value.ScopeVersion, Is.EqualTo(4));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CustomerSignature_EmptySigner_FailsWithSignerRequired(string signer)
    {
        var result = CustomerSignature.Create(signer, Now, 1);

        Assert.That(result.Error, Is.EqualTo(VisitErrors.SignerRequired));
    }

    [Test]
    public void CustomerSignature_SameValues_AreEqual()
    {
        var created = CustomerSignature.Create("Jan Klient", Now, 2).Value;
        var restored = CustomerSignature.Restore("Jan Klient", Now, 2);

        Assert.That(created, Is.EqualTo(restored));
    }

    [Test]
    public void CustomerSignature_DifferentScopeVersion_AreNotEqual()
    {
        var first = CustomerSignature.Restore("Jan Klient", Now, 1);
        var second = CustomerSignature.Restore("Jan Klient", Now, 2);

        Assert.That(first, Is.Not.EqualTo(second));
    }

    // ---------- VisitOutcome ----------

    [Test]
    public void VisitOutcome_Fixed_HasNoReason()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VisitOutcome.Fixed.Type, Is.EqualTo(VisitOutcomeType.Fixed));
            Assert.That(VisitOutcome.Fixed.Reason, Is.Null);
        });
    }

    [Test]
    public void VisitOutcome_CreateFixedWithoutReason_Succeeds()
    {
        var result = VisitOutcome.Create(VisitOutcomeType.Fixed, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Type, Is.EqualTo(VisitOutcomeType.Fixed));
        });
    }

    [TestCase(VisitOutcomeType.NeedsFollowUp, null)]
    [TestCase(VisitOutcomeType.NeedsFollowUp, "")]
    [TestCase(VisitOutcomeType.NotFixed, null)]
    [TestCase(VisitOutcomeType.NotFixed, "")]
    public void VisitOutcome_WithoutReason_FailsWithReasonRequired(VisitOutcomeType type, string? reason)
    {
        var result = VisitOutcome.Create(type, reason);

        Assert.That(result.Error, Is.EqualTo(VisitErrors.ReasonRequired));
    }

    [TestCase(VisitOutcomeType.NeedsFollowUp)]
    [TestCase(VisitOutcomeType.NotFixed)]
    public void VisitOutcome_WithReason_KeepsReason(VisitOutcomeType type)
    {
        var result = VisitOutcome.Create(type, "Brak części w aucie");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Type, Is.EqualTo(type));
            Assert.That(result.Value.Reason, Is.EqualTo("Brak części w aucie"));
        });
    }

    [Test]
    public void VisitOutcome_SameValues_AreEqual()
    {
        var first = VisitOutcome.Create(VisitOutcomeType.NotFixed, "Do wymiany").Value;
        var second = VisitOutcome.Create(VisitOutcomeType.NotFixed, "Do wymiany").Value;

        Assert.That(first, Is.EqualTo(second));
    }

    // ---------- VisitScope ----------

    [Test]
    public void VisitScope_SameContent_AreEqual()
    {
        var part = new PartId("P-1");
        var a = VisitScope.Empty.WithPart(part, Quantity.Create(2).Value);
        var b = VisitScope.Empty.WithPart(part, Quantity.Create(2).Value);

        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void VisitScope_Empty_IsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VisitScope.Empty.IsEmpty, Is.True);
            Assert.That(VisitScope.Empty.Activities, Is.Empty);
            Assert.That(VisitScope.Empty.Parts, Is.Empty);
        });
    }

    [Test]
    public void VisitScope_WithActivity_IsNotEmptyAndOriginalUnchanged()
    {
        var original = VisitScope.Empty;

        var changed = original.WithActivity(new Activity("A01", "Diagnostyka", Now));

        Assert.Multiple(() =>
        {
            Assert.That(changed.IsEmpty, Is.False);
            Assert.That(changed.Activities, Has.Count.EqualTo(1));
            Assert.That(original.IsEmpty, Is.True);
        });
    }

    [Test]
    public void VisitScope_WithPartOnly_IsNotEmpty()
    {
        var scope = VisitScope.Empty.WithPart(Filter, Qty(1));

        Assert.That(scope.IsEmpty, Is.False);
    }

    [Test]
    public void VisitScope_WithSamePartTwice_SumsQuantityInOneLine()
    {
        var scope = VisitScope.Empty
            .WithPart(Filter, Qty(1))
            .WithPart(Filter, Qty(2));

        Assert.That(scope.Parts, Is.EqualTo(new[] { new UsedPart(Filter, Qty(3)) }));
    }

    [Test]
    public void VisitScope_WithDifferentParts_KeepsSeparateLinesInOrder()
    {
        var scope = VisitScope.Empty
            .WithPart(Filter, Qty(1))
            .WithPart(Valve, Qty(2));

        Assert.That(scope.Parts, Is.EqualTo(new[] { new UsedPart(Filter, Qty(1)), new UsedPart(Valve, Qty(2)) }));
    }

    [Test]
    public void VisitScope_WithPart_DoesNotChangeOriginal()
    {
        var original = VisitScope.Empty.WithPart(Filter, Qty(1));

        _ = original.WithPart(Filter, Qty(5));

        Assert.That(original.Parts, Is.EqualTo(new[] { new UsedPart(Filter, Qty(1)) }));
    }

    private static Quantity Qty(int value) => Quantity.Create(value).Value;
}
