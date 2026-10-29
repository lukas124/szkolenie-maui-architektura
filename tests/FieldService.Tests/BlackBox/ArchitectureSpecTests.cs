using System.Reflection;
using FieldService.Application.Abstractions;
using NetArchTest.Rules;

namespace FieldService.Tests.BlackBox;

/// <summary>
/// Testy architektury wyprowadzone z karty Bloku 02 i słownika nazw:
/// reguła zależności (strzałki tylko do środka) i „tylko UI zna MAUI”.
/// </summary>
[TestFixture]
public class ArchitectureSpecTests
{
    private static readonly Assembly DomainAssembly = typeof(FieldService.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(FieldService.Application.AssemblyMarker).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(FieldService.Infrastructure.AssemblyMarker).Assembly;
    private static readonly Assembly PresentationAssembly = typeof(FieldService.Presentation.AssemblyMarker).Assembly;

    private const string DomainNs = "FieldService.Domain";
    private const string ApplicationNs = "FieldService.Application";
    private const string InfrastructureNs = "FieldService.Infrastructure";
    private const string PresentationNs = "FieldService.Presentation";
    private const string UiNs = "FieldService.UI";
    private const string Maui = "Microsoft.Maui";

    // --- Strażnicy: reguła sprawdzająca złą przestrzeń nazw byłaby „zielona od razu” ---

    [Test]
    public void KazdyProjekt_MaTypyWeWlasnejPrzestrzeniNazw()
    {
        Assert.That(Types.InAssembly(DomainAssembly).That().ResideInNamespace(DomainNs).GetTypes(), Is.Not.Empty);
        Assert.That(Types.InAssembly(ApplicationAssembly).That().ResideInNamespace(ApplicationNs).GetTypes(), Is.Not.Empty);
        Assert.That(Types.InAssembly(InfrastructureAssembly).That().ResideInNamespace(InfrastructureNs).GetTypes(), Is.Not.Empty);
        Assert.That(Types.InAssembly(PresentationAssembly).That().ResideInNamespace(PresentationNs).GetTypes(), Is.Not.Empty);
    }

    [Test]
    public void Presentation_ZawieraViewModele()
    {
        var viewModels = Types.InAssembly(PresentationAssembly)
            .That().AreClasses().And().HaveNameEndingWith("ViewModel")
            .GetTypes()
            .Select(t => t.Name)
            .ToList();

        Assert.That(viewModels, Does.Contain("VisitsListViewModel"));
        Assert.That(viewModels, Does.Contain("LoginViewModel"));
        Assert.That(viewModels, Does.Contain("VisitDetailsViewModel"));
    }

    // --- Domain: zero zależności ---

    [Test]
    public void Domain_NieZaleznyOdZadnegoInnegoProjektuAniFrameworka()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, PresentationNs, UiNs, Maui,
                "CommunityToolkit", "Microsoft.Extensions", "System.Net.Http")
            .GetResult();

        AssertSuccessful(result, "Domain");
    }

    [Test]
    public void Domain_NieReferujeZestawowInnychProjektowAniPakietow()
    {
        var forbidden = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.StartsWith("FieldService.", StringComparison.Ordinal)
                        || n.StartsWith(Maui, StringComparison.Ordinal)
                        || n.StartsWith("CommunityToolkit", StringComparison.Ordinal)
                        || n.StartsWith("Microsoft.Extensions", StringComparison.Ordinal))
            .ToList();

        Assert.That(forbidden, Is.Empty);
    }

    // --- Application: zna tylko Domain ---

    [Test]
    public void Application_NieZnaInfrastructurePresentationUiAniMaui()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, PresentationNs, UiNs, Maui)
            .GetResult();

        AssertSuccessful(result, "Application");
    }

    [Test]
    public void Application_NieZnaHttpClient()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("System.Net.Http")
            .GetResult();

        AssertSuccessful(result, "Application (System.Net.Http)");
    }

    [Test]
    public void Application_NieReferujeZestawowZewnetrznychPierscieni()
    {
        var forbidden = ApplicationAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n is InfrastructureNs or PresentationNs or UiNs
                        || n.StartsWith(Maui, StringComparison.Ordinal)
                        || n == "System.Net.Http")
            .ToList();

        Assert.That(forbidden, Is.Empty);
    }

    [Test]
    public void PrzypadkiUzycia_HandlerySaWApplication()
    {
        var handlers = Types.InAssembly(ApplicationAssembly)
            .That().AreClasses().And().HaveNameEndingWith("Handler")
            .GetTypes()
            .ToList();

        Assert.That(handlers.Select(t => t.Name), Does.Contain("GetTodayVisitsHandler"));
        Assert.That(handlers.Select(t => t.Name), Does.Contain("SignInHandler"));
        Assert.That(handlers.Where(t => t.Namespace is null || !t.Namespace.StartsWith(ApplicationNs, StringComparison.Ordinal)), Is.Empty);
    }

    [Test]
    public void PrzypadkiUzycia_NieMieszkajaWPresentationAniWDomain()
    {
        var inPresentation = Types.InAssembly(PresentationAssembly)
            .That().AreClasses().And().HaveNameEndingWith("Handler")
            .GetTypes();
        var inDomain = Types.InAssembly(DomainAssembly)
            .That().AreClasses().And().HaveNameEndingWith("Handler")
            .GetTypes();

        Assert.That(inPresentation, Is.Empty);
        Assert.That(inDomain, Is.Empty);
    }

    [Test]
    public void PortBramyWizyt_ImplementowanyTylkoWInfrastructure()
    {
        var inApplication = Types.InAssembly(ApplicationAssembly)
            .That().AreClasses().And().ImplementInterface(typeof(IVisitsGateway))
            .GetTypes();
        var inPresentation = Types.InAssembly(PresentationAssembly)
            .That().AreClasses().And().ImplementInterface(typeof(IVisitsGateway))
            .GetTypes();
        var inInfrastructure = Types.InAssembly(InfrastructureAssembly)
            .That().AreClasses().And().ImplementInterface(typeof(IVisitsGateway))
            .GetTypes();

        Assert.That(inApplication, Is.Empty);
        Assert.That(inPresentation, Is.Empty);
        Assert.That(inInfrastructure, Is.Not.Empty);
    }

    [Test]
    public void IVisitsGateway_JestPortemWApplication()
    {
        Assert.That(typeof(IVisitsGateway).Assembly, Is.SameAs(ApplicationAssembly));
        Assert.That(typeof(IVisitsGateway).IsInterface, Is.True);
    }

    // --- Presentation: bez MAUI, bez UI, bez Infrastructure ---

    [Test]
    public void Presentation_NieZnaMauiUiAniInfrastructure()
    {
        var result = Types.InAssembly(PresentationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Maui, UiNs, InfrastructureNs)
            .GetResult();

        AssertSuccessful(result, "Presentation");
    }

    [Test]
    public void Presentation_NieReferujeZestawowMauiUiAniInfrastructure()
    {
        var forbidden = PresentationAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n is InfrastructureNs or UiNs
                        || n.StartsWith(Maui, StringComparison.Ordinal))
            .ToList();

        Assert.That(forbidden, Is.Empty);
    }

    [Test]
    public void ViewModele_NieZalezaOdInfrastructure()
    {
        var result = Types.InAssembly(PresentationAssembly)
            .That().HaveNameEndingWith("ViewModel")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNs)
            .GetResult();

        AssertSuccessful(result, "ViewModele");
    }

    [Test]
    public void ViewModele_NieZalezaOdMaui()
    {
        var result = Types.InAssembly(PresentationAssembly)
            .That().HaveNameEndingWith("ViewModel")
            .ShouldNot()
            .HaveDependencyOnAny(Maui, UiNs)
            .GetResult();

        AssertSuccessful(result, "ViewModele (MAUI)");
    }

    // --- Infrastructure: zna Application i Domain, nie zna prezentacji ---

    [Test]
    public void Infrastructure_NieZnaPresentationUiAniMaui()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(PresentationNs, UiNs, Maui)
            .GetResult();

        AssertSuccessful(result, "Infrastructure");
    }

    [Test]
    public void Infrastructure_DtoZApiNieSaPubliczne()
    {
        // DTO z API nie wychodzi z Infrastructure (karta Bloku 02, quiz: VisitDto → VisitSummary).
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().HaveNameEndingWith("Dto")
            .ShouldNot()
            .BePublic()
            .GetResult();

        AssertSuccessful(result, "Infrastructure (DTO)");
    }

    private static void AssertSuccessful(TestResult result, string rule)
    {
        Assert.That(result.IsSuccessful, Is.True,
            () => $"{rule}: naruszenia w typach: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
