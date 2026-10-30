using CommunityToolkit.Mvvm.Messaging;
using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Presentation.Messages;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Visits;

// VisitDetailsViewModel odbiera SyncCompletedMessage przez WeakReferenceMessenger (bez zdarzenia singletona)
[TestFixture]
public sealed class VisitDetailsViewModelSyncTests
{
    private VisitDetailsFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new VisitDetailsFixture();

    [TearDown]
    public void TearDown() => WeakReferenceMessenger.Default.Reset();

    private static SyncCompletedMessage SyncCompleted() =>
        new(new SyncResult(Sent: 1, Pending: 0, Conflicts: Array.Empty<VisitId>(), Rejected: Array.Empty<VisitId>()));

    // Po wejściu na ekran SyncCompletedMessage → szczegóły wczytane na nowo
    [Test]
    public async Task Receive_SyncCompletedAfterNavigation_ReloadsDetails()
    {
        var viewModel = _fixture.CreateViewModel();
        await VisitDetailsFixture.NavigateAsync(viewModel);
        var loadsBefore = _fixture.DetailsLoads;

        WeakReferenceMessenger.Default.Send(SyncCompleted());

        Assert.That(() => _fixture.DetailsLoads, Is.GreaterThan(loadsBefore).After(2000, 20));
    }

    // Rejestracja dopiero przy wejściu – przed OnNavigatedToAsync komunikat nic nie robi
    [Test]
    public void Receive_SyncCompletedBeforeNavigation_DoesNothing()
    {
        var viewModel = _fixture.CreateViewModel();

        WeakReferenceMessenger.Default.Send(SyncCompleted());

        Assert.That(_fixture.DetailsLoads, Is.EqualTo(0));
        GC.KeepAlive(viewModel);
    }

    // Po OnClosed() ViewModel jest wyrejestrowany – komunikat nie wczytuje szczegółów
    [Test]
    public async Task Receive_SyncCompletedAfterClose_DoesNothing()
    {
        var viewModel = _fixture.CreateViewModel();
        await VisitDetailsFixture.NavigateAsync(viewModel);
        viewModel.OnClosed();
        var loadsAfterClose = _fixture.DetailsLoads;

        WeakReferenceMessenger.Default.Send(SyncCompleted());
        await Task.Delay(100);

        Assert.That(_fixture.DetailsLoads, Is.EqualTo(loadsAfterClose));
    }

    // Po OnClosed() posłaniec nie trzyma już ViewModelu jako odbiorcy
    [Test]
    public async Task OnClosed_AfterNavigation_UnregistersFromMessenger()
    {
        var viewModel = _fixture.CreateViewModel();
        await VisitDetailsFixture.NavigateAsync(viewModel);
        Assert.That(WeakReferenceMessenger.Default.IsRegistered<SyncCompletedMessage>(viewModel), Is.True);

        viewModel.OnClosed();

        Assert.That(WeakReferenceMessenger.Default.IsRegistered<SyncCompletedMessage>(viewModel), Is.False);
    }
}
