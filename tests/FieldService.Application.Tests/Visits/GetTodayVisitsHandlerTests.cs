using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// Lista wizyt na dziś: kolejność od najwcześniejszej, status z repliki tylko dla wizyt zmienionych w telefonie,
// wersja zawsze z serwera, IsLate po nałożeniu.
[TestFixture]
public class GetTodayVisitsHandlerTests
{
    private Mock<IVisitsGateway> _gateway = null!;
    private Mock<IVisitReplica> _replica = null!;
    private Dictionary<Guid, VisitStatus> _changedOnPhone = null!;
    private FixedClock _clock = null!;
    private GetTodayVisitsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IVisitsGateway>();
        _replica = new Mock<IVisitReplica>();
        _changedOnPhone = [];
        _replica.Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _changedOnPhone);
        _clock = new FixedClock();
        _handler = new GetTodayVisitsHandler(_gateway.Object, _replica.Object, _clock);
    }

    [Test]
    public async Task GatewayVisits_AreOrderedFromEarliest()
    {
        var late = Summary(Guid.NewGuid(), _clock.Now.AddHours(3));
        var early = Summary(Guid.NewGuid(), _clock.Now.AddHours(1));
        GatewayReturns(late, early);

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Value.Select(v => v.Id), Is.EqualTo(new[] { early.Id, late.Id }));
    }

    [Test]
    public async Task VisitNotChangedOnPhone_KeepsGatewayStatusAndVersion()
    {
        var summary = Summary(Guid.NewGuid(), _clock.Now.AddHours(1), VisitStatus.Planned, version: 4);
        GatewayReturns(summary);

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Single().Status, Is.EqualTo(VisitStatus.Planned));
            Assert.That(result.Value.Single().Version, Is.EqualTo(4));
        });
    }

    // Wersja na liście to wersja serwera – ta trafi kiedyś do If-Match; lokalna wersja tylko pilnuje zapisów w telefonie
    [Test]
    public async Task VisitChangedOnPhone_OverlaysLocalStatusAndKeepsServerVersion()
    {
        var id = Guid.NewGuid();
        _changedOnPhone[id] = VisitStatus.InProgress;
        GatewayReturns(Summary(id, _clock.Now.AddHours(1), VisitStatus.Planned, version: 1));

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        var visit = result.Value.Single();
        Assert.Multiple(() =>
        {
            Assert.That(visit.Status, Is.EqualTo(VisitStatus.InProgress));
            Assert.That(visit.Version, Is.EqualTo(1));
            Assert.That(visit.CustomerName, Is.EqualTo("Jan Klient"));
        });
    }

    [Test]
    public async Task PlannedInPast_IsLate()
    {
        GatewayReturns(Summary(Guid.NewGuid(), _clock.Now.AddMinutes(-15)));

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Value.Single().IsLate, Is.True);
    }

    [Test]
    public async Task PlannedInFuture_IsNotLate()
    {
        GatewayReturns(Summary(Guid.NewGuid(), _clock.Now.AddMinutes(15)));

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Value.Single().IsLate, Is.False);
    }

    [Test]
    public async Task InProgressFromGateway_IsNotLate()
    {
        GatewayReturns(Summary(Guid.NewGuid(), _clock.Now.AddHours(-1), VisitStatus.InProgress));

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Value.Single().IsLate, Is.False);
    }

    // Wizyta „Planned” na serwerze, ale technik już ją rozpoczął lokalnie – po nałożeniu repliki nie jest spóźniona.
    [Test]
    public async Task PlannedInPastButStartedLocally_IsNotLate()
    {
        var id = Guid.NewGuid();
        _changedOnPhone[id] = VisitStatus.InProgress;
        GatewayReturns(Summary(id, _clock.Now.AddHours(-1), VisitStatus.Planned));

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Value.Single().IsLate, Is.False);
    }

    [Test]
    public async Task GatewayError_ReturnsGatewayError()
    {
        var offline = new Error("Network.Offline", ErrorType.Network);
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(offline));

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(offline));
    }

    private void GatewayReturns(params VisitSummary[] summaries) =>
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success(summaries));

    private static VisitSummary Summary(Guid id, DateTimeOffset plannedStart,
        VisitStatus status = VisitStatus.Planned, int version = 1) => new(
        id, "ZL-2026-0042", status, plannedStart, plannedStart.AddHours(2), "Jan Klient", "ul. Polna 1, Kraków",
        "Pompa ciepła AW-8", "Nie grzeje wody", false, "https://example.invalid/thumb.png", version);
}
