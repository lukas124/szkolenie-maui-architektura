using System.Globalization;
using FieldService.Application;
using FieldService.Application.Abstractions;
using FieldService.Infrastructure;
using FieldService.Presentation;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits.Completion;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace FieldService.Tests;

// Test architektury: graf obiektów składa się bez emulatora i bez stron MAUI
public class ContainerTests
{
    private static ServiceProvider BuildProvider()
    {
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        localizer.Setup(l => l.CurrentCulture).Returns(CultureInfo.GetCultureInfo("pl-PL"));

        var services = new ServiceCollection();
        services.AddApplication()
            .AddInfrastructure(new ApiOptions { BaseUrl = "https://example.invalid/" })
            .AddPresentation();

        // Porty z warstwy UI – atrapy
        services.AddSingleton(Mock.Of<IDialogService>());
        services.AddSingleton(localizer.Object);
        services.AddSingleton(Mock.Of<IPreferencesStore>());
        services.AddSingleton(Mock.Of<INavigationService>());
        services.AddSingleton(Mock.Of<IAppPaths>());
        services.AddSingleton(Mock.Of<IPermissionService>());
        services.AddSingleton(Mock.Of<IPhotoCapture>());
        services.AddSingleton(Mock.Of<ISessionStore>());
        services.AddSingleton(Mock.Of<ISessionExpiry>());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Test]
    public void Container_BuildsWithValidation()
    {
        Assert.DoesNotThrow(() =>
        {
            using var provider = BuildProvider();
        });
    }

    [Test]
    public void SyncScheduler_IsSingleton_AndProcessorIsNewPerRun()
    {
        using var provider = BuildProvider();

        var processors = provider.GetRequiredService<Func<FieldService.Infrastructure.Outbox.OutboxProcessor>>();
        var firstScheduler = provider.GetRequiredService<ISyncScheduler>();
        var firstProcessor = processors();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<ISyncScheduler>(), Is.SameAs(firstScheduler));
            // procesor (z HttpClient bramy) nie żyje w singletonie – nowy na każdy przebieg
            Assert.That(processors(), Is.Not.SameAs(firstProcessor));
        });
    }

    [Test]
    public void RouteMap_ContainsVisitRoutesAndLogin()
    {
        using var provider = BuildProvider();

        var map = provider.GetRequiredService<RouteMap>();

        Assert.That(map.Entries.Keys, Is.SupersetOf(new[]
        {
            typeof(LoginRoute), typeof(VisitsListRoute), typeof(VisitDetailsRoute), typeof(CompleteVisitRoute),
            typeof(VisitHistoryRoute), typeof(VanStockRoute), typeof(ReservePartRoute),
        }));
    }

    [Test]
    public void RouteMap_IsSingleton()
    {
        using var provider = BuildProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.That(first.ServiceProvider.GetRequiredService<RouteMap>(),
            Is.SameAs(second.ServiceProvider.GetRequiredService<RouteMap>()));
    }

    [Test]
    public void AllRoutedViewModels_Resolve()
    {
        using var provider = BuildProvider();
        var map = provider.GetRequiredService<RouteMap>();

        Assert.That(map.Entries, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var (route, viewModelType) in map.Entries)
            {
                // Jak usługa nawigacji: ViewModel tworzony w zakresie
                using var scope = provider.CreateScope();
                object? viewModel = null;
                Assert.DoesNotThrow(() => viewModel = scope.ServiceProvider.GetRequiredService(viewModelType),
                    $"{route.Name} → {viewModelType.Name}");
                Assert.That(viewModel, Is.InstanceOf(viewModelType), route.Name);
            }
        });
    }

    [Test]
    public void CompletionDraft_IsSharedWithinScope_AndNewPerScope()
    {
        using var provider = BuildProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var a1 = first.ServiceProvider.GetRequiredService<CompletionDraft>();
        var a2 = first.ServiceProvider.GetRequiredService<CompletionDraft>();
        var b = second.ServiceProvider.GetRequiredService<CompletionDraft>();

        // Jeden stan na otwarcie kreatora
        Assert.That(a2, Is.SameAs(a1));
        Assert.That(b, Is.Not.SameAs(a1));
    }

    [Test]
    public void CompletionDraft_FromRoot_Throws()
    {
        using var provider = BuildProvider();

        // ValidateScopes: Scoped z korzenia to błąd (działałby jak singleton)
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<CompletionDraft>());
    }
}
