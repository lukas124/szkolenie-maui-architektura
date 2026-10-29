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
    public void Application_DoesNotDependOnInfrastructure() =>
        AssertNoDependency(Application, "FieldService.Infrastructure", "System.Net.Http", "SQLite");

    [Test]
    public void Application_DoesNotDependOnPresentationOrUi() =>
        AssertNoDependency(Application, "FieldService.Presentation", "FieldService.UI", Maui);

    [Test]
    public void Presentation_DoesNotReferenceMaui() =>
        AssertNoDependency(Presentation, Maui, "FieldService.UI");

    [Test]
    public void Presentation_DoesNotDependOnInfrastructure() =>
        AssertNoDependency(Presentation, "FieldService.Infrastructure");

    // ViewModel dostaje model odczytu (VisitDetails), nigdy agregat ani repozytorium – zmiany tylko przez przypadki użycia
    [Test]
    public void Presentation_DoesNotUseAggregateOrRepository()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var forbidden = new[] { typeof(FieldService.Domain.Visits.Visit), typeof(FieldService.Domain.Visits.IVisitRepository) };

        var offenders = Presentation.GetTypes()
            .SelectMany(t => t.GetFields(all).Select(f => f.FieldType)
                .Concat(t.GetProperties(all).Select(p => p.PropertyType))
                .Concat(t.GetConstructors(all).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
                .Concat(t.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
                .Where(forbidden.Contains)
                .Select(_ => t.FullName))
            .Distinct()
            .ToList();

        Assert.That(offenders, Is.Empty);
    }

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
