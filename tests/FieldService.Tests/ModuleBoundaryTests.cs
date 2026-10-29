using System.Reflection;
using NetArchTest.Rules;
using NUnit.Framework;

namespace FieldService.Tests;

// Granice modułów według mapy kontekstów (moduł ↛ moduł, w każdym pierścieniu):
// Parts/Inventory ↛ Visits, Orders · Orders ↛ Visits, Parts, Inventory · Visits ↛ Orders (Visits → Parts dozwolone – Magazyn jest upstreamem)
[TestFixture]
public class ModuleBoundaryTests
{
    private static readonly string[] Layers = { "Domain", "Application", "Infrastructure", "Presentation" };

    private static IEnumerable<TestCaseData> Assemblies()
    {
        yield return new TestCaseData(typeof(FieldService.Domain.AssemblyMarker).Assembly).SetArgDisplayNames("Domain");
        yield return new TestCaseData(typeof(FieldService.Application.AssemblyMarker).Assembly).SetArgDisplayNames("Application");
        yield return new TestCaseData(typeof(FieldService.Infrastructure.AssemblyMarker).Assembly).SetArgDisplayNames("Infrastructure");
        yield return new TestCaseData(typeof(FieldService.Presentation.AssemblyMarker).Assembly).SetArgDisplayNames("Presentation");
    }

    // Przestrzenie modułu we wszystkich pierścieniach, np. "Visits" → FieldService.Domain.Visits, FieldService.Application.Visits, …
    private static string[] ModuleNamespaces(params string[] modules) =>
        modules.SelectMany(module => Layers.Select(layer => $"FieldService.{layer}.{module}")).ToArray();

    private static string Describe(TestResult result) =>
        "Naruszenie granic modułów: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());

    [TestCaseSource(nameof(Assemblies))]
    public void PartsModule_InAssembly_DoesNotDependOnVisitsOrOrders(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceContaining(".Parts")
            .Or().ResideInNamespaceContaining(".Inventory")
            .ShouldNot().HaveDependencyOnAny(ModuleNamespaces("Visits", "Orders"))
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, Describe(result));
    }

    [TestCaseSource(nameof(Assemblies))]
    public void OrdersModule_InAssembly_DoesNotDependOnVisitsPartsOrInventory(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceContaining(".Orders")
            .ShouldNot().HaveDependencyOnAny(ModuleNamespaces("Visits", "Parts", "Inventory"))
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, Describe(result));
    }

    [TestCaseSource(nameof(Assemblies))]
    public void VisitsModule_InAssembly_DoesNotDependOnOrders(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceContaining(".Visits")
            .ShouldNot().HaveDependencyOnAny(ModuleNamespaces("Orders"))
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, Describe(result));
    }

    [Test]
    public void ModuleNamespaces_Visits_CoversEveryLayer()
    {
        // Pilnuje, że reguły obejmują wszystkie pierścienie (pusta lista = test zawsze zielony)
        Assert.That(ModuleNamespaces("Visits"), Is.EqualTo(new[]
        {
            "FieldService.Domain.Visits", "FieldService.Application.Visits",
            "FieldService.Infrastructure.Visits", "FieldService.Presentation.Visits",
        }));
    }
}
