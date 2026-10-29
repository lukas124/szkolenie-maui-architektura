using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// Zapytanie o szczegóły: najpierw replika; gdy brak – brama, SeedAsync, ponowny odczyt repliki; PhotoCount ze sklepu zdjęć.
// Refresh (otwarcie ekranu): najpierw brama i SeedAsync; bez sieci – to, co w replice.
[TestFixture]
public class GetVisitDetailsHandlerTests
{
    private Mock<IVisitReplica> _replica = null!;
    private Mock<IVisitsGateway> _gateway = null!;
    private FakeVisitPhotoStore _photos = null!;
    private GetVisitDetailsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _replica = new Mock<IVisitReplica>();
        _gateway = new Mock<IVisitsGateway>();
        _photos = new FakeVisitPhotoStore();
        _handler = new GetVisitDetailsHandler(_replica.Object, _gateway.Object, _photos);
    }

    [Test]
    public async Task VisitInReplica_ReturnsDetailsWithoutCallingGateway()
    {
        var id = new VisitId(Guid.NewGuid());
        var details = Details(id);
        _replica.Setup(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(details);

        var result = await _handler.HandleAsync(new GetVisitDetails(id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(details));
            _gateway.Verify(g => g.GetVisitAsync(It.IsAny<VisitId>(), It.IsAny<CancellationToken>()), Times.Never);
            _replica.Verify(r => r.SeedAsync(It.IsAny<RemoteVisit>(), It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task VisitInReplica_PhotoCountComesFromPhotoStore()
    {
        var id = new VisitId(Guid.NewGuid());
        _replica.Setup(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Details(id));
        await _photos.AddAsync(id, "/zdjecia/1.jpg", FixedClock.DefaultNow, CancellationToken.None);
        await _photos.AddAsync(id, "/zdjecia/2.jpg", FixedClock.DefaultNow, CancellationToken.None);
        await _photos.AddAsync(new VisitId(Guid.NewGuid()), "/zdjecia/obca.jpg", FixedClock.DefaultNow, CancellationToken.None);

        var result = await _handler.HandleAsync(new GetVisitDetails(id), CancellationToken.None);

        Assert.That(result.Value.PhotoCount, Is.EqualTo(2));
    }

    [Test]
    public async Task VisitNotInReplica_FetchesFromGatewaySeedsAndReadsReplicaAgain()
    {
        var id = new VisitId(Guid.NewGuid());
        var details = Details(id);
        var remote = Remote(id);
        _replica.SetupSequence(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VisitDetails?)null)
            .ReturnsAsync(details);
        _gateway.Setup(g => g.GetVisitAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoteVisit>.Success(remote));
        await _photos.AddAsync(id, "/zdjecia/1.jpg", FixedClock.DefaultNow, CancellationToken.None);

        var result = await _handler.HandleAsync(new GetVisitDetails(id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(details with { PhotoCount = 1 }));
            _replica.Verify(r => r.SeedAsync(remote, It.IsAny<CancellationToken>()), Times.Once);
            _replica.Verify(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>()), Times.Exactly(2));
        });
    }

    [Test]
    public async Task Refresh_GatewayOk_SeedsReplicaAndReadsIt()
    {
        var id = new VisitId(Guid.NewGuid());
        var remote = Remote(id);
        _replica.Setup(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Details(id));
        _gateway.Setup(g => g.GetVisitAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoteVisit>.Success(remote));

        var result = await _handler.HandleAsync(new GetVisitDetails(id, Refresh: true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            _replica.Verify(r => r.SeedAsync(remote, It.IsAny<CancellationToken>()), Times.Once);
            _replica.Verify(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Test]
    public async Task Refresh_Offline_ReturnsReplica()
    {
        var id = new VisitId(Guid.NewGuid());
        var details = Details(id);
        _replica.Setup(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _gateway.Setup(g => g.GetVisitAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoteVisit>.Failure(new Error("Network.Unavailable", ErrorType.Network)));

        var result = await _handler.HandleAsync(new GetVisitDetails(id, Refresh: true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(details));
            _replica.Verify(r => r.SeedAsync(It.IsAny<RemoteVisit>(), It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task GatewayError_ReturnsGatewayErrorWithoutSeeding()
    {
        var id = new VisitId(Guid.NewGuid());
        var offline = new Error("Network.Offline", ErrorType.Network);
        _replica.Setup(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((VisitDetails?)null);
        _gateway.Setup(g => g.GetVisitAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoteVisit>.Failure(offline));

        var result = await _handler.HandleAsync(new GetVisitDetails(id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(offline));
            _replica.Verify(r => r.SeedAsync(It.IsAny<RemoteVisit>(), It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task GatewayNotFound_ReturnsNotFound()
    {
        var id = new VisitId(Guid.NewGuid());
        _replica.Setup(r => r.GetDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((VisitDetails?)null);
        _gateway.Setup(g => g.GetVisitAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RemoteVisit>.Failure(VisitErrors.NotFound));

        var result = await _handler.HandleAsync(new GetVisitDetails(id), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotFound));
    }

    private static VisitDetails Details(VisitId id) => new(
        id.Value, "ZL-2026-0042", VisitStatus.Planned, FixedClock.DefaultNow, FixedClock.DefaultNow.AddHours(2),
        new CustomerSnapshot("Jan Klient", "+48 600 100 200", "ul. Polna 1, Kraków"),
        "Pompa ciepła AW-8", "Nie grzeje wody", "https://example.invalid/thumb.png",
        [], [], false, null, null, 0);

    private static RemoteVisit Remote(VisitId id) => new(
        TestVisits.Planned(id),
        new VisitSnapshot("ZL-2026-0042", new CustomerSnapshot("Jan Klient", "+48 600 100 200", "ul. Polna 1, Kraków"),
            "Pompa ciepła AW-8", "Nie grzeje wody", "https://example.invalid/thumb.png"));
}
