using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Tests.Navigation;

public class RouteMapTests
{
    private sealed class FirstViewModel : BaseViewModel
    {
    }

    private sealed class SecondViewModel : BaseViewModel
    {
    }

    [Test]
    public void Map_AnyRoute_ReturnsSameMapForChaining()
    {
        var map = new RouteMap();

        var returned = map.Map<LoginRoute, FirstViewModel>();

        Assert.That(returned, Is.SameAs(map));
    }

    [Test]
    public void ViewModelFor_MappedRoute_ReturnsViewModelType()
    {
        var map = new RouteMap()
            .Map<LoginRoute, FirstViewModel>()
            .Map<VisitDetailsRoute, SecondViewModel>();

        Assert.That(map.ViewModelFor(typeof(LoginRoute)), Is.EqualTo(typeof(FirstViewModel)));
        Assert.That(map.ViewModelFor(typeof(VisitDetailsRoute)), Is.EqualTo(typeof(SecondViewModel)));
    }

    [Test]
    public void ViewModelFor_UnmappedRoute_ReturnsNull()
    {
        var map = new RouteMap().Map<LoginRoute, FirstViewModel>();

        Assert.That(map.ViewModelFor(typeof(CompleteVisitRoute)), Is.Null);
    }

    [Test]
    public void ViewModelFor_RouteInstanceType_ReturnsViewModelType()
    {
        var map = new RouteMap().Map<VisitDetailsRoute, FirstViewModel>();
        IRoute route = new VisitDetailsRoute(new VisitId(Guid.NewGuid()));

        // Usługa nawigacji pyta o typ instancji trasy
        Assert.That(map.ViewModelFor(route.GetType()), Is.EqualTo(typeof(FirstViewModel)));
    }

    [Test]
    public void Entries_NewMap_IsEmpty()
    {
        Assert.That(new RouteMap().Entries, Is.Empty);
    }

    [Test]
    public void Entries_AfterMapping_ContainsAllPairs()
    {
        var map = new RouteMap()
            .Map<VisitsListRoute, FirstViewModel>()
            .Map<CompleteVisitRoute, SecondViewModel>();

        Assert.That(map.Entries, Has.Count.EqualTo(2));
        Assert.That(map.Entries[typeof(VisitsListRoute)], Is.EqualTo(typeof(FirstViewModel)));
        Assert.That(map.Entries[typeof(CompleteVisitRoute)], Is.EqualTo(typeof(SecondViewModel)));
    }
}
