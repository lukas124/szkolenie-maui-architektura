using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common; // ZAŁOŻENIE: Result<T>, Error w tej przestrzeni nazw
using FieldService.Domain.Visits;
using FieldService.TestDoubles;
using Moq;

namespace FieldService.Application.Tests.Visits;

// Atrapy portów GetTodayVisitsHandler w jednym miejscu – gdy konstruktor albo porty różnią się od założeń,
// poprawka dotyczy tylko tego pliku.
internal sealed class TodayVisitsFixture
{
    // 28.10.2026 12:00 czasu lokalnego (południe – z dala od granicy dnia)
    public static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0,
        TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 28, 12, 0, 0)));

    // migawka zapisana tego samego dnia o 7:42
    public static readonly DateTimeOffset SnapshotSavedAt = new(2026, 10, 28, 7, 42, 0, Now.Offset);

    public Mock<IVisitsGateway> Gateway { get; } = new();
    public Mock<IVisitReplica> Replica { get; } = new();
    public Mock<IVisitListSnapshotStore> Snapshots { get; } = new();
    public FixedClock Clock { get; } = new(Now); // ZAŁOŻENIE: FixedClock(DateTimeOffset now)

    public TodayVisitsFixture()
    {
        NoLocalChanges();
        NoSnapshot();
    }

    // ZAŁOŻENIE: konstruktor GetTodayVisitsHandler(IVisitsGateway, IVisitReplica, IVisitListSnapshotStore, IClock)
    public GetTodayVisitsHandler CreateHandler() =>
        new(Gateway.Object, Replica.Object, Snapshots.Object, Clock);

    // ZAŁOŻENIE: IVisitsGateway.GetTodayAsync(CancellationToken) → Result<IReadOnlyList<VisitSummary>>
    public void GatewayReturns(params VisitSummary[] visits) =>
        Gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success(visits));

    public void GatewayFails(Error error) =>
        Gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(error));

    public void VerifyGatewayNeverCalled() =>
        Gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Never);

    // ZAŁOŻENIE: IVisitListSnapshotStore.GetAsync(CancellationToken) → VisitListSnapshot? (Visits, SavedAt)
    public void SnapshotContains(DateTimeOffset savedAt, params VisitSummary[] visits) =>
        Snapshots.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitListSnapshot(visits, savedAt));

    public void NoSnapshot() =>
        Snapshots.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((VisitListSnapshot?)null);

    // ZAŁOŻENIE: IVisitReplica.GetLocallyChangedStatusesAsync(CancellationToken) → IReadOnlyDictionary<VisitId, VisitStatus>
    public void LocalStatuses(IReadOnlyDictionary<VisitId, VisitStatus> statuses) =>
        Replica.Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statuses.ToDictionary(s => s.Key.Value, s => s.Value));

    public void NoLocalChanges() => LocalStatuses(new Dictionary<VisitId, VisitStatus>());

    // ZAŁOŻENIE: kolejność parametrów VisitSummary (Id, WorkOrderNumber, Status, PlannedStart, CustomerName, City,
    // DeviceName, ThumbnailUrl, IsLate)
    public static VisitSummary Summary(Guid id, VisitStatus status, DateTimeOffset plannedStart) =>
        new(id, $"ZL-2610-{id.ToString()[..4]}", status, plannedStart, plannedStart.AddHours(1), "Jan Klient", "Piaseczno",
            "Pompa ciepła 8 kW", "Nie grzeje", false, $"https://api.test/api/images/{id:N}?size=thumb", 1);
}
