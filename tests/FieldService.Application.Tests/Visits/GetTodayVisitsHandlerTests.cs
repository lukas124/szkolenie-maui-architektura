using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Tests.Visits;

// Przypadek użycia testujemy bez sieci i bez MAUI: port IVisitsGateway i IClock to atrapy
[TestFixture]
public class GetTodayVisitsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 30, 0, TimeSpan.FromHours(1));

    private readonly Mock<IVisitsGateway> _gateway = new();
    private readonly Mock<IClock> _clock = new();

    [SetUp]
    public void SetUp() => _clock.Setup(c => c.Now).Returns(Now);

    private GetTodayVisitsHandler CreateHandler() => new(_gateway.Object, _clock.Object);

    private static VisitSummary Visit(string customer, VisitStatus status, DateTimeOffset start) =>
        new(Guid.NewGuid(), "ZL-2611-1001", status, start, start.AddMinutes(90), customer, "ul. Lipowa 12, Piaseczno",
            "Pompa ciepła 8 kW", "Nie grzeje", IsBrigade: false, ThumbnailUrl: "https://example.com/t.png", Version: 1);

    private void GatewayReturns(params VisitSummary[] visits) =>
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success(visits));

    [Test]
    public async Task HandleAsync_ReturnsVisitsOrderedByPlannedStart()
    {
        GatewayReturns(Visit("Popołudnie", VisitStatus.Planned, Now.AddHours(4)), Visit("Rano", VisitStatus.Completed, Now.AddHours(-2)));

        var result = await CreateHandler().HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Value.Select(v => v.CustomerName), Is.EqualTo(new[] { "Rano", "Popołudnie" }));
    }

    [Test]
    public async Task HandleAsync_MarksPlannedVisitAsLate_WhenWindowAlreadyStarted()
    {
        GatewayReturns(
            Visit("Spóźniona", VisitStatus.Planned, Now.AddMinutes(-15)),
            Visit("Później", VisitStatus.Planned, Now.AddMinutes(30)),
            Visit("W trakcie", VisitStatus.InProgress, Now.AddMinutes(-60)));

        var result = await CreateHandler().HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Value.Where(v => v.IsLate).Select(v => v.CustomerName), Is.EqualTo(new[] { "Spóźniona" }));
    }

    [Test]
    public async Task HandleAsync_ReturnsGatewayError_WhenNoNetwork()
    {
        var error = new Error("Visits.Network", ErrorType.Network);
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(error));

        var result = await CreateHandler().HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(error));
    }
}
