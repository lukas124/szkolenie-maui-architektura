namespace FieldService.Infrastructure.Tests;

// Test dymny (ang. smoke test): projekt testowy buduje się i widzi testowany projekt
[TestFixture]
public class SmokeTests
{
    [Test]
    public void AssemblyMarker_BelongsToInfrastructureProject()
    {
        var assembly = typeof(FieldService.Infrastructure.AssemblyMarker).Assembly;

        Assert.That(assembly.GetName().Name, Is.EqualTo("FieldService.Infrastructure"));
    }
}
