namespace FieldService.Presentation.Tests;

// Test dymny (ang. smoke test): projekt testowy buduje się i widzi testowany projekt
[TestFixture]
public class SmokeTests
{
    [Test]
    public void AssemblyMarker_BelongsToPresentationProject()
    {
        var assembly = typeof(FieldService.Presentation.AssemblyMarker).Assembly;

        Assert.That(assembly.GetName().Name, Is.EqualTo("FieldService.Presentation"));
    }
}
