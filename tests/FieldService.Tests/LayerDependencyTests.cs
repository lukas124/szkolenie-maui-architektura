using System.Reflection;
using NetArchTest.Rules;

namespace FieldService.Tests;

// Testy architektury: kto kogo może znać (reguła zależności). Sprawdzają strukturę kodu, nie zachowanie.
// UI (projekt MAUI) nie jest tu ładowany – reguła „tylko UI zna MAUI” = żaden inny projekt nie zależy od Microsoft.Maui.
[TestFixture]
public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(FieldService.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly Application = typeof(FieldService.Application.AssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(FieldService.Infrastructure.AssemblyMarker).Assembly;
    private static readonly Assembly Presentation = typeof(FieldService.Presentation.AssemblyMarker).Assembly;

    private const string Maui = "Microsoft.Maui";

    [Test]
    public void Domain_DoesNotDependOnOtherProjects() =>
        AssertNoDependency(Domain, "FieldService.Application", "FieldService.Infrastructure", "FieldService.Presentation", "FieldService.UI", Maui);

    // Przypadki użycia nie znają adapterów ani szczegółów, którymi adaptery się zajmują (tu: HTTP)
    [Test]
    [Ignore("Ćwiczenie 02: usuń Ignore i uzupełnij test")]
    public void Application_DoesNotDependOnInfrastructure()
    {
        // TODO: Ćwiczenie 02 – Application nie zależy od "FieldService.Infrastructure" ani od "System.Net.Http"
        // (użyj AssertNoDependency jak w testach obok)
        Assert.Fail("Test do uzupełnienia");
    }

    [Test]
    public void Application_DoesNotDependOnPresentationOrUi() =>
        AssertNoDependency(Application, "FieldService.Presentation", "FieldService.UI", Maui);

    [Test]
    [Ignore("Ćwiczenie 02: usuń Ignore i uzupełnij test")]
    public void Presentation_DoesNotReferenceMaui()
    {
        // TODO: Ćwiczenie 02 – Presentation nie zależy od Microsoft.Maui ani od FieldService.UI
        Assert.Fail("Test do uzupełnienia");
    }

    [Test]
    public void Presentation_DoesNotDependOnInfrastructure() =>
        AssertNoDependency(Presentation, "FieldService.Infrastructure");

    [Test]
    public void Infrastructure_DoesNotDependOnPresentationOrMaui() =>
        AssertNoDependency(Infrastructure, "FieldService.Presentation", "FieldService.UI", Maui);

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"{assembly.GetName().Name} zależy od {string.Join(", ", forbidden)}: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
