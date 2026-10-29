using System.Reflection;
using System.Text.RegularExpressions;
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
        AssertNoDependency(Presentation, "FieldService.Infrastructure", "System.Net.Http");

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

    // Lokalizator usług: ViewModel dostaje zależności przez konstruktor, kontener zna tylko korzeń kompozycji (UI)
    [Test]
    [Ignore("Ćwiczenie 09: usuń Ignore po uzupełnieniu TODO")]
    public void Presentation_DoesNotUseServiceProvider() =>
        AssertNoDependency(Presentation, "System.IServiceProvider", "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory",
            "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");

    // Czas w modelu i przypadkach użycia tylko z IClock albo parametru now (NetArchTest nie widzi wywołań – skan źródeł)
    [Test]
    public void DomainAndApplication_DoNotReadSystemClock()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root.GetFiles("FieldService.slnx").Length == 0) root = root.Parent!;
        var obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";

        var offenders = new[] { "src/FieldService.Domain", "src/FieldService.Application" }
            .SelectMany(d => Directory.GetFiles(Path.Combine(root.FullName, d), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains(obj) && Regex.IsMatch(File.ReadAllText(f), @"DateTime(Offset)?\.(Utc)?Now"))
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
