using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// Lista bez sieci: każda pobrana lista trafia do migawki, przy braku sieci lista pochodzi z migawki.
[TestFixture]
public class GetTodayVisitsSnapshotTests
{
    private static readonly Error Offline = new("Visits.Network", ErrorType.Network);

    private Mock<IVisitsGateway> _gateway = null!;
    private Mock<IVisitReplica> _replica = null!;
    private Mock<IVisitListSnapshotStore> _snapshots = null!;
    private FixedClock _clock = null!;
    private GetTodayVisitsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IVisitsGateway>();
        _replica = new Mock<IVisitReplica>();
        _replica.Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VisitStatus>());
        _snapshots = new Mock<IVisitListSnapshotStore>();
        _clock = new FixedClock();
        _handler = new GetTodayVisitsHandler(_gateway.Object, _replica.Object, _snapshots.Object, _clock);
    }

    [Test]
    public async Task GatewaySuccess_SavesSnapshotWithClockTime_AndIsNotOffline()
    {
        var visit = Summary(Guid.NewGuid());
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success([visit]));

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Value.OfflineSince, Is.Null);
        _snapshots.Verify(s => s.SaveAsync(
            It.Is<VisitListSnapshot>(x => x.SavedAt == _clock.Now && x.Visits.Single().Id == visit.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task NetworkError_WithSnapshot_ReturnsSnapshotVisitsWithStatusFromReplica()
    {
        var id = Guid.NewGuid();
        var savedAt = _clock.Now.AddHours(-2);
        GatewayFails(Offline);
        _snapshots.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitListSnapshot([Summary(id)], savedAt));
        _replica.Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VisitStatus> { [id] = VisitStatus.InProgress });

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.OfflineSince, Is.EqualTo(savedAt));
            Assert.That(result.Value.Visits.Single().Status, Is.EqualTo(VisitStatus.InProgress));
        });
    }

    [Test]
    public async Task NetworkError_WithoutSnapshot_ReturnsNetworkError()
    {
        GatewayFails(Offline);

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(Offline));
    }

    // Migawka tylko przy braku sieci – inny błąd (np. sesja) to nie „pokaż stare dane”
    [Test]
    public async Task OtherError_DoesNotUseSnapshot()
    {
        var unauthorized = new Error("Visits.Unauthorized", ErrorType.Unauthorized);
        GatewayFails(unauthorized);
        _snapshots.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitListSnapshot([Summary(Guid.NewGuid())], _clock.Now));

        var result = await _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(unauthorized));
    }

    private void GatewayFails(Error error) =>
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(error));

    private VisitSummary Summary(Guid id) => new(
        id, "ZL-2026-0042", VisitStatus.Planned, _clock.Now.AddHours(1), _clock.Now.AddHours(3), "Jan Klient",
        "ul. Polna 1, Kraków", "Pompa ciepła AW-8", "Nie grzeje wody", false, "https://example.invalid/thumb.png", 1);
}
