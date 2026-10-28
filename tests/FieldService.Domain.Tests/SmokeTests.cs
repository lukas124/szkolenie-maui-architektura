namespace FieldService.Domain.Tests;

// Test dymny (ang. smoke test): projekt testowy buduje się i widzi testowany projekt
[TestFixture]
public class SmokeTests
{
    [Test]
    public void AssemblyMarker_BelongsToDomainProject()
    {
        var assembly = typeof(FieldService.Domain.AssemblyMarker).Assembly;

        Assert.That(assembly.GetName().Name, Is.EqualTo("FieldService.Domain"));
    }
}
