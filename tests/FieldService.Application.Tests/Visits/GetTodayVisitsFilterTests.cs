using FieldService.Application.Abstractions;
using FieldService.Application.Tests.Contract;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;

namespace FieldService.Application.Tests.Visits;

// Filtr listy wizyt w GetTodayVisitsHandler: OnlyOverdue (OverdueVisitSpecification) i Status (po nałożeniu statusu z repliki)
[TestFixture]
public class GetTodayVisitsFilterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.FromHours(1));

    // Zaplanowana, okno minęło – spóźniona
    private static readonly Guid LatePlannedId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    // Zaplanowana na później – nie spóźniona
    private static readonly Guid FuturePlannedId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    // W toku, okno minęło – nie spóźniona (nie jest już zaplanowana)
    private static readonly Guid InProgressId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    // Zakończona rano
    private static readonly Guid CompletedId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private Mock<IVisitsGateway> _gateway = null!;
    private Mock<IVisitReplica> _replica = null!;
    private Mock<IClock> _clock = null!;
    private Dictionary<Guid, VisitStatus> _localStatuses = null!;

    [SetUp]
    public void SetUp()
    {
        _localStatuses = new Dictionary<Guid, VisitStatus>();

        _gateway = new Mock<IVisitsGateway>();
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContractTestData.Ok<IReadOnlyList<VisitSummary>>(new[]
            {
                ContractTestData.Summary(LatePlannedId, VisitStatus.Planned, Now.AddHours(-1)),
                ContractTestData.Summary(FuturePlannedId, VisitStatus.Planned, Now.AddHours(3)),
                ContractTestData.Summary(InProgressId, VisitStatus.InProgress, Now.AddHours(-2)),
                ContractTestData.Summary(CompletedId, VisitStatus.Completed, Now.AddHours(-4)),
            }));

        _replica = new Mock<IVisitReplica>();
        _replica.Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _localStatuses);

        _clock = new Mock<IClock>();
        _clock.Setup(c => c.Now).Returns(Now);
    }

    private GetTodayVisitsHandler CreateHandler() => new(_gateway.Object, _replica.Object, Mock.Of<IVisitListSnapshotStore>(), _clock.Object);

    private async Task<Guid[]> HandleAndGetIdsAsync(GetTodayVisits query)
    {
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        return result.Value.Visits.Select(v => v.Id).ToArray();
    }

    [Test]
    public async Task HandleAsync_NoFilter_ReturnsAllVisits()
    {
        var ids = await HandleAndGetIdsAsync(new GetTodayVisits());

        Assert.That(ids, Is.EquivalentTo(new[] { LatePlannedId, FuturePlannedId, InProgressId, CompletedId }));
    }

    [Test]
    public async Task HandleAsync_OnlyOverdue_ReturnsOnlyLateVisits()
    {
        var ids = await HandleAndGetIdsAsync(new GetTodayVisits(OnlyOverdue: true));

        Assert.That(ids, Is.EqualTo(new[] { LatePlannedId }));
    }

    [Test]
    public async Task HandleAsync_OnlyOverdue_ReturnedVisitsAreMarkedLate()
    {
        var result = await CreateHandler().HandleAsync(new GetTodayVisits(OnlyOverdue: true), CancellationToken.None);

        Assert.That(result.Value.Visits.All(v => v.IsLate), Is.True);
    }

    [TestCase(VisitStatus.Planned, new[] { 1, 2 })]
    [TestCase(VisitStatus.InProgress, new[] { 3 })]
    [TestCase(VisitStatus.Completed, new[] { 4 })]
    [TestCase(VisitStatus.Cancelled, new int[0])]
    public async Task HandleAsync_StatusFilter_ReturnsOnlyThatStatus(VisitStatus status, int[] expectedIds)
    {
        var ids = await HandleAndGetIdsAsync(new GetTodayVisits(Status: status));

        Assert.That(ids, Is.EquivalentTo(expectedIds.Select(ToGuid)));
    }

    [Test]
    public async Task HandleAsync_OnlyOverdueAndPlannedStatus_ReturnsLatePlannedOnly()
    {
        var ids = await HandleAndGetIdsAsync(new GetTodayVisits(OnlyOverdue: true, Status: VisitStatus.Planned));

        Assert.That(ids, Is.EqualTo(new[] { LatePlannedId }));
    }

    [Test]
    public async Task HandleAsync_OnlyOverdueAndInProgressStatus_ReturnsEmpty()
    {
        // Spóźniona może być tylko zaplanowana – połączenie z „W toku” nic nie zwraca
        var ids = await HandleAndGetIdsAsync(new GetTodayVisits(OnlyOverdue: true, Status: VisitStatus.InProgress));

        Assert.That(ids, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_StatusChangedInReplica_FiltersByReplicaStatus()
    {
        // Serwer jeszcze nie wie, że technik rozpoczął wizytę – liczy się status z repliki
        _localStatuses[FuturePlannedId] = VisitStatus.InProgress;

        var ids = await HandleAndGetIdsAsync(new GetTodayVisits(Status: VisitStatus.InProgress));

        Assert.That(ids, Is.EquivalentTo(new[] { FuturePlannedId, InProgressId }));
    }

    [Test]
    public async Task HandleAsync_StatusChangedInReplica_ExcludedFromServerStatus()
    {
        _localStatuses[LatePlannedId] = VisitStatus.Completed;

        var ids = await HandleAndGetIdsAsync(new GetTodayVisits(Status: VisitStatus.Planned));

        Assert.That(ids, Is.EqualTo(new[] { FuturePlannedId }));
    }

    private static Guid ToGuid(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
}
