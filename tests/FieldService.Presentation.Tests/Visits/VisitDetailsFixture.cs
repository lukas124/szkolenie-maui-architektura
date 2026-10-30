using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits;
using FieldService.TestDoubles;

namespace FieldService.Presentation.Tests.Visits;

// Szczegóły wizyty do testów komunikatu synchronizacji i LeakTests. Wczytanie szczegółów = wywołanie repliki albo bramy.
internal sealed class VisitDetailsFixture
{
    public static readonly VisitId VisitId = new(Guid.Parse("5c2d9e10-4b7a-4f3e-9d61-0a1b2c3d4e01"));

    public Mock<IVisitsGateway> Gateway { get; } = new();
    public Mock<IVisitReplica> Replica { get; } = new();
    public Mock<IVisitPhotoStore> Photos { get; } = new();
    public Mock<IPermissionService> Permissions { get; } = new();
    public Mock<IVisitFormatter> Formatter { get; } = new();
    public Mock<IUserFeedback> Feedback { get; } = new();
    public Mock<INavigationService> Navigation { get; } = new();
    public FixedClock Clock { get; } = new(FixedClock.DefaultNow);

    // liczba wywołań portów, z których czyta GetVisitDetailsHandler
    public int DetailsLoads => Replica.Invocations.Count + Gateway.Invocations.Count;

    public VisitDetailsFixture()
    {
        // bez sieci: szczegóły z repliki (brak wizyty → komunikat błędu z atrapy IUserFeedback)
        Gateway.Setup(g => g.GetVisitAsync(It.IsAny<VisitId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoteVisit>.Failure(new Error("Api.Network", ErrorType.Network)));
    }

    public VisitDetailsViewModel CreateViewModel() =>
        new(new GetVisitDetailsHandler(Replica.Object, Gateway.Object, Photos.Object),
            new StartVisitHandler(new InMemoryVisitRepository(), Clock),
            new AddVisitPhotoHandler(Permissions.Object, Mock.Of<IPhotoCapture>(), Photos.Object, Clock),
            Permissions.Object, Formatter.Object, Feedback.Object, Navigation.Object);

    public static Task NavigateAsync(VisitDetailsViewModel viewModel) =>
        viewModel.OnNavigatedToAsync(new VisitDetailsRoute(VisitId));
}
