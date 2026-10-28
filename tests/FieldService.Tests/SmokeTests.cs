namespace FieldService.Tests;

// Test dymny architektury: środek aplikacji (Domain) nie zna żadnego innego projektu FieldService.
// Pełne reguły zależności między pierścieniami dopiszemy w Bloku 02 (LayerDependencyTests).
[TestFixture]
public class SmokeTests
{
    [Test]
    public void Domain_DoesNotReferenceOtherFieldServiceProjects()
    {
        var references = typeof(FieldService.Domain.AssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null && name.StartsWith("FieldService"));

        Assert.That(references, Is.Empty);
    }
}
