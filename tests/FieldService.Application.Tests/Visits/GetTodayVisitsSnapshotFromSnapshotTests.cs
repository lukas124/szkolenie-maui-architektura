using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common; // ZAŁOŻENIE: Error, ErrorType w tej przestrzeni nazw
using FieldService.Domain.Visits;
using NUnit.Framework;

namespace FieldService.Application.Tests.Visits;

// Stale-while-revalidate po stronie zapytania: GetTodayVisits(FromSnapshot: true) i SnapshotAt
[TestFixture]
public sealed class GetTodayVisitsSnapshotFromSnapshotTests
{
    private static readonly Guid FirstId = Guid.Parse("0b6f5c7e-1111-4c2a-9f00-000000000001");
    private static readonly Guid SecondId = Guid.Parse("0b6f5c7e-2222-4c2a-9f00-000000000002");
    private static readonly Error Network = new("Api.Network", ErrorType.Network);

    private TodayVisitsFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new TodayVisitsFixture();

    private Task<Result<TodayVisits>> HandleAsync(GetTodayVisits query) =>
        _fixture.CreateHandler().HandleAsync(query, CancellationToken.None);

    // FromSnapshot i brak migawki → błąd VisitsErrors.NoSnapshot, brama niewołana
    [Test]
    public async Task HandleAsync_FromSnapshotWithoutSnapshot_ReturnsNoSnapshot()
    {
        var result = await HandleAsync(new GetTodayVisits(FromSnapshot: true));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(VisitsErrors.NoSnapshot));
        _fixture.VerifyGatewayNeverCalled();
    }

    // Błąd NoSnapshot ma kod "Visits.NoSnapshot" i typ NotFound
    [Test]
    public void NoSnapshot_Always_HasCodeAndNotFoundType()
    {
        Assert.That(VisitsErrors.NoSnapshot.Code, Is.EqualTo("Visits.NoSnapshot"));
        Assert.That(VisitsErrors.NoSnapshot.Type, Is.EqualTo(ErrorType.NotFound));
    }

    // FromSnapshot z migawką → wizyty z migawki i SnapshotAt = chwila zapisu, bez bramy
    [Test]
    public async Task HandleAsync_FromSnapshotWithSnapshot_ReturnsSnapshotVisitsWithoutGateway()
    {
        _fixture.SnapshotContains(TodayVisitsFixture.SnapshotSavedAt,
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(2)),
            TodayVisitsFixture.Summary(SecondId, VisitStatus.InProgress, TodayVisitsFixture.Now.AddHours(-1)));

        var result = await HandleAsync(new GetTodayVisits(FromSnapshot: true));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Visits.Select(v => v.Id), Is.EquivalentTo(new[] { FirstId, SecondId }));
        Assert.That(result.Value.SnapshotAt, Is.EqualTo(TodayVisitsFixture.SnapshotSavedAt));
        _fixture.VerifyGatewayNeverCalled();
    }

    // Z migawki – status z repliki (lokalna zmiana) wygrywa ze statusem zapisanym w migawce
    [Test]
    public async Task HandleAsync_FromSnapshotWithLocalChange_UsesReplicaStatus()
    {
        _fixture.SnapshotContains(TodayVisitsFixture.SnapshotSavedAt,
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(1)));
        _fixture.LocalStatuses(new Dictionary<VisitId, VisitStatus> { [new VisitId(FirstId)] = VisitStatus.InProgress });

        var result = await HandleAsync(new GetTodayVisits(FromSnapshot: true));

        Assert.That(result.Value.Visits.Single().Status, Is.EqualTo(VisitStatus.InProgress));
    }

    // Z migawki – filtr statusu działa jak dla danych z sieci
    [Test]
    public async Task HandleAsync_FromSnapshotWithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        _fixture.SnapshotContains(TodayVisitsFixture.SnapshotSavedAt,
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(2)),
            TodayVisitsFixture.Summary(SecondId, VisitStatus.InProgress, TodayVisitsFixture.Now.AddHours(-1)));

        var result = await HandleAsync(new GetTodayVisits(Status: VisitStatus.InProgress, FromSnapshot: true));

        Assert.That(result.Value.Visits.Select(v => v.Id), Is.EqualTo(new[] { SecondId }));
    }

    // Z migawki – IsLate liczone względem zegara: zaplanowana 3 h temu jest spóźniona, za 3 h – nie
    [Test]
    public async Task HandleAsync_FromSnapshot_ComputesIsLateFromClock()
    {
        _fixture.SnapshotContains(TodayVisitsFixture.SnapshotSavedAt,
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(-3)),
            TodayVisitsFixture.Summary(SecondId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(3)));

        var result = await HandleAsync(new GetTodayVisits(FromSnapshot: true));

        Assert.That(result.Value.Visits.Single(v => v.Id == FirstId).IsLate, Is.True);
        Assert.That(result.Value.Visits.Single(v => v.Id == SecondId).IsLate, Is.False);
    }

    // Z migawki – OnlyOverdue zostawia tylko spóźnione
    [Test]
    public async Task HandleAsync_FromSnapshotOnlyOverdue_ReturnsOnlyLateVisits()
    {
        _fixture.SnapshotContains(TodayVisitsFixture.SnapshotSavedAt,
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(-3)),
            TodayVisitsFixture.Summary(SecondId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(3)));

        var result = await HandleAsync(new GetTodayVisits(OnlyOverdue: true, FromSnapshot: true));

        Assert.That(result.Value.Visits.Select(v => v.Id), Is.EqualTo(new[] { FirstId }));
    }

    // Świeże dane z serwera → SnapshotAt = null
    [Test]
    public async Task HandleAsync_NetworkSucceeds_ReturnsVisitsWithoutSnapshotAt()
    {
        _fixture.SnapshotContains(TodayVisitsFixture.SnapshotSavedAt,
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(2)));
        _fixture.GatewayReturns(
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(2)),
            TodayVisitsFixture.Summary(SecondId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(4)));

        var result = await HandleAsync(new GetTodayVisits());

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Visits, Has.Count.EqualTo(2));
        Assert.That(result.Value.SnapshotAt, Is.Null);
    }

    // Brak sieci i migawka → lista z migawki z SnapshotAt = chwila zapisu
    [Test]
    public async Task HandleAsync_NetworkErrorWithSnapshot_ReturnsSnapshotWithSnapshotAt()
    {
        _fixture.GatewayFails(Network);
        _fixture.SnapshotContains(TodayVisitsFixture.SnapshotSavedAt,
            TodayVisitsFixture.Summary(FirstId, VisitStatus.Planned, TodayVisitsFixture.Now.AddHours(2)));

        var result = await HandleAsync(new GetTodayVisits());

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Visits.Select(v => v.Id), Is.EqualTo(new[] { FirstId }));
        Assert.That(result.Value.SnapshotAt, Is.EqualTo(TodayVisitsFixture.SnapshotSavedAt));
    }

    // Brak sieci i brak migawki → błąd
    [Test]
    public async Task HandleAsync_NetworkErrorWithoutSnapshot_ReturnsFailure()
    {
        _fixture.GatewayFails(Network);

        var result = await HandleAsync(new GetTodayVisits());

        Assert.That(result.IsSuccess, Is.False);
    }
}
