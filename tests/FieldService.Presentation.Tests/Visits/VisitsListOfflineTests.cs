using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.State;
using FieldService.Presentation.Visits;
using FieldService.TestDoubles;
using CommunityToolkit.Mvvm.Messaging;

namespace FieldService.Presentation.Tests.Visits;

// Lista bez sieci: wizyty z migawki i tekst „Bez sieci – dane z …” zamiast komunikatu o błędzie
[TestFixture]
public class VisitsListOfflineTests
{
    private FixedClock _clock = null!;
    private Mock<IVisitListSnapshotStore> _snapshots = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private VisitsListViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedClock();
        var gateway = new Mock<IVisitsGateway>();
        gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(new Error("Visits.Network", ErrorType.Network)));
        var replica = new Mock<IVisitReplica>();
        replica.Setup(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, VisitStatus>());
        _snapshots = new Mock<IVisitListSnapshotStore>();
        _feedback = new Mock<IUserFeedback>();
        var appState = new Mock<IAppState>();
        appState.Setup(a => a.Filter).Returns(new VisitFilterState());
        var formatter = new VisitFormatter(new TestLocalizer(), _clock);

        _viewModel = new VisitsListViewModel(
            new GetTodayVisitsHandler(gateway.Object, replica.Object, _snapshots.Object, _clock),
            new VisitItemFactory(formatter), formatter, _feedback.Object, Mock.Of<INavigationService>(), appState.Object,
            Mock.Of<ISyncScheduler>());
    }

    [TearDown]
    public void TearDown() => WeakReferenceMessenger.Default.UnregisterAll(_viewModel);

    [Test]
    public async Task OnNavigatedTo_NoNetworkWithSnapshot_ShowsSnapshotAndOfflineTime()
    {
        var savedAt = new DateTimeOffset(_clock.Now.Year, _clock.Now.Month, _clock.Now.Day, 7, 45, 0, _clock.Now.Offset);
        _snapshots.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitListSnapshot([Summary()], savedAt));

        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Visits, Has.Count.EqualTo(1));
            Assert.That(_viewModel.LastUpdated, Is.EqualTo("Bez sieci – dane z 7:45"));
            _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
        });
    }

    [Test]
    public async Task OnNavigatedTo_NoNetworkWithoutSnapshot_ShowsError()
    {
        await _viewModel.OnNavigatedToAsync(new VisitsListRoute());

        _feedback.Verify(f => f.ShowErrorAsync(It.Is<Error>(e => e.Type == ErrorType.Network)), Times.Once);
    }

    // Migawka z poprzedniego dnia – z datą, żeby technik nie wziął wczorajszej listy za dzisiejszą
    [Test]
    public void FormatOfflineSince_OtherDay_IncludesDate()
    {
        var formatter = new VisitFormatter(new TestLocalizer(), _clock);
        var yesterday = new DateTimeOffset(_clock.Now.Date.AddDays(-1).AddHours(16).AddMinutes(5), _clock.Now.Offset);

        Assert.That(formatter.FormatOfflineSince(yesterday), Is.EqualTo($"Bez sieci – dane z {yesterday:d.MM} 16:05"));
    }

    private VisitSummary Summary() => new(
        Guid.NewGuid(), "ZL-2026-0042", VisitStatus.Planned, _clock.Now.AddHours(1), _clock.Now.AddHours(3), "Jan Klient",
        "ul. Polna 1, Kraków", "Pompa ciepła AW-8", "Nie grzeje wody", false, "https://example.invalid/thumb.png", 1);
}
