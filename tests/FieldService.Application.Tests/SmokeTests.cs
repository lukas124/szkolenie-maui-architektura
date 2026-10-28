namespace FieldService.Application.Tests;

// Test dymny (ang. smoke test): projekt testowy buduje się i widzi testowany projekt
[TestFixture]
public class SmokeTests
{
    [Test]
    public void AssemblyMarker_BelongsToApplicationProject()
    {
        var assembly = typeof(FieldService.Application.AssemblyMarker).Assembly;

        Assert.That(assembly.GetName().Name, Is.EqualTo("FieldService.Application"));
    }
}
