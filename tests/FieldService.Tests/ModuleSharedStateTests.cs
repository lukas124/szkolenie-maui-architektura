using System.Reflection;
using NetArchTest.Rules;

namespace FieldService.Tests;

// Uzupełnienie ModuleBoundaryTests: wspólne przestrzenie (porty, stan aplikacji, komunikaty) leżą poza modułami,
// więc granicę modułu Orders sprawdzamy także po nazwach typów modułu Visits. Do tego strażnik: reguła nie może
// przechodzić „na pusto”, gdy po zmianie nazw przestrzeni test nie wybiera żadnego typu.
[TestFixture]
public class ModuleSharedStateTests
{
    private static readonly Assembly Application = typeof(FieldService.Application.AssemblyMarker).Assembly;
    private static readonly Assembly Presentation = typeof(FieldService.Presentation.AssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(FieldService.Infrastructure.AssemblyMarker).Assembly;

    [Test]
    public void Orders_DoesNotUseVisitsPortsStateOrMessages()
    {
        string[] visitsTypes =
        [
            "FieldService.Application.Abstractions.IVisitsGateway", "FieldService.Application.Abstractions.IVisitReplica",
            "FieldService.Application.Abstractions.IFlowDraftStore", "FieldService.Presentation.State",
            "FieldService.Presentation.Messages.FilterChangedMessage",
            // wysyłka zmian wizyt (outbox) – też dane modułu Visits, choć leżą poza jego przestrzenią nazw
            "FieldService.Application.Abstractions.ISyncScheduler", "FieldService.Presentation.Messages.SyncCompletedMessage",
            "FieldService.Infrastructure.Outbox"
        ];

        foreach (var assembly in new[] { Application, Infrastructure, Presentation })
        {
            var result = Types.InAssembly(assembly).That().ResideInNamespace($"{assembly.GetName().Name}.Orders")
                .ShouldNot().HaveDependencyOnAny(visitsTypes).GetResult();
            Assert.That(result.IsSuccessful, Is.True,
                $"{assembly.GetName().Name}.Orders zależy od typów modułu Visits: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [TestCase("FieldService.Application.Orders")]
    [TestCase("FieldService.Infrastructure.Orders")]
    [TestCase("FieldService.Presentation.Orders")]
    [TestCase("FieldService.Application.Parts")]
    [TestCase("FieldService.Infrastructure.Inventory")]
    [TestCase("FieldService.Presentation.Visits")]
    public void ModuleNamespace_HasTypes(string ns)
    {
        var assembly = new[] { Application, Infrastructure, Presentation }.Single(a => ns.StartsWith(a.GetName().Name + "."));

        Assert.That(Types.InAssembly(assembly).That().ResideInNamespace(ns).GetTypes(), Is.Not.Empty,
            $"Przestrzeń {ns} nie ma typów – reguły granic modułu sprawdzałyby pusty zbiór");
    }
}
