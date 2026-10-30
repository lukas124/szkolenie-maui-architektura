using CommunityToolkit.Mvvm.Messaging;
using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Presentation.Messages;

namespace FieldService.Presentation.Tests.Visits.Completion;

// Kreator odbiera wynik wysyłki zmian (SyncCompletedMessage) przez WeakReferenceMessenger: serwer odrzucił zmiany tej
// wizyty → kreator się zamyka
[TestFixture]
public sealed class CompleteVisitFlowSyncTests
{
    private CompleteVisitFlowFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new CompleteVisitFlowFixture();

    [TearDown]
    public void TearDown() => WeakReferenceMessenger.Default.Reset();

    private static void Send(SyncResult result) => WeakReferenceMessenger.Default.Send(new SyncCompletedMessage(result));

    // Konflikt tej wizyty → kreator zamknięty
    [Test]
    public async Task Receive_ConflictForThisVisit_ClosesWizard()
    {
        var viewModel = _fixture.CreateViewModel();
        await CompleteVisitFlowFixture.StartAsync(viewModel);

        Send(new SyncResult(0, 0, [_fixture.VisitId], []));

        Assert.That(() => _fixture.Navigation.Invocations.Count(i => i.Method.Name == "CloseModalAsync"), Is.EqualTo(1).After(2000, 20));
    }

    // Zmiany tej wizyty odrzucone po ponowieniach → kreator zamknięty
    [Test]
    public async Task Receive_RejectedThisVisit_ClosesWizard()
    {
        var viewModel = _fixture.CreateViewModel();
        await CompleteVisitFlowFixture.StartAsync(viewModel);

        Send(new SyncResult(0, 0, [], [_fixture.VisitId]));

        Assert.That(() => _fixture.Navigation.Invocations.Count(i => i.Method.Name == "CloseModalAsync"), Is.EqualTo(1).After(2000, 20));
    }

    // Wysłane bez problemów albo konflikt innej wizyty → kreator zostaje
    [Test]
    public async Task Receive_OtherVisitOrSent_KeepsWizard()
    {
        var viewModel = _fixture.CreateViewModel();
        await CompleteVisitFlowFixture.StartAsync(viewModel);

        Send(new SyncResult(3, 0, [new VisitId(Guid.NewGuid())], []));
        await Task.Delay(100);

        _fixture.Navigation.Verify(n => n.CloseModalAsync(), Times.Never);
    }

    // Rejestracja w konstruktorze – kreator jest odbiorcą od razu po utworzeniu
    [Test]
    public void Constructor_Always_RegistersForSyncResults()
    {
        var viewModel = _fixture.CreateViewModel();

        Assert.That(WeakReferenceMessenger.Default.IsRegistered<SyncCompletedMessage>(viewModel), Is.True);
    }

    // OnClosed() wyrejestrowuje kreator
    [Test]
    public void OnClosed_Always_UnregistersFromMessenger()
    {
        var viewModel = _fixture.CreateViewModel();

        viewModel.OnClosed();

        Assert.That(WeakReferenceMessenger.Default.IsRegistered<SyncCompletedMessage>(viewModel), Is.False);
    }
}
