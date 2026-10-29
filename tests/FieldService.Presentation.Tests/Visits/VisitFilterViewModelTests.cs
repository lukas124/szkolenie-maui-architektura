using FieldService.Presentation.Feedback;
using CommunityToolkit.Mvvm.Messaging;
using FieldService.Domain.Visits;
using FieldService.Presentation.Messages;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.State;
using FieldService.Presentation.Visits;

namespace FieldService.Presentation.Tests.Visits;

// Modal filtra: pola z appState przy wejściu, Apply/Clear → ApplyFilter → FilterChangedMessage → CloseModalAsync
[TestFixture]
public class VisitFilterViewModelTests
{
    private readonly Mock<IUserFeedback> _feedback = new();

    private VisitFilterState _filter = null!;
    private Mock<IAppState> _appState = null!;
    private Mock<INavigationService> _navigation = null!;
    private List<string> _events = null!;
    private TestRecipient _recipient = null!;

    [SetUp]
    public void SetUp()
    {
        _events = new List<string>();
        _filter = new VisitFilterState { OnlyOverdue = true, Status = VisitStatus.Planned };

        _appState = new Mock<IAppState>();
        _appState.Setup(a => a.Filter).Returns(_filter);
        _appState.Setup(a => a.ApplyFilter(It.IsAny<bool>(), It.IsAny<VisitStatus?>()))
            .Callback(() => _events.Add("apply"));

        _navigation = new Mock<INavigationService>();
        _navigation.Setup(n => n.CloseModalAsync())
            .Callback(() => _events.Add("close"))
            .Returns(Task.CompletedTask);

        _recipient = new TestRecipient(_events);
        WeakReferenceMessenger.Default.Register<FilterChangedMessage>(_recipient);
    }

    [TearDown]
    public void TearDown() => WeakReferenceMessenger.Default.UnregisterAll(_recipient);

    private async Task<VisitFilterViewModel> CreateAndEnterAsync()
    {
        var viewModel = new VisitFilterViewModel(_appState.Object, _navigation.Object, _feedback.Object, new TestLocalizer());
        await viewModel.OnNavigatedToAsync(new VisitFilterRoute());
        return viewModel;
    }

    [Test]
    public async Task OnNavigatedToAsync_FilterInAppState_FillsFields()
    {
        var viewModel = await CreateAndEnterAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.OnlyOverdue, Is.True);
            Assert.That(viewModel.Status, Is.EqualTo(VisitStatus.Planned));
        });
    }

    [Test]
    public void Statuses_Always_AllThenEveryStatus()
    {
        var viewModel = new VisitFilterViewModel(_appState.Object, _navigation.Object, _feedback.Object, new TestLocalizer());

        Assert.That(viewModel.Statuses, Is.EqualTo(new VisitStatus?[]
        {
            null, VisitStatus.Planned, VisitStatus.InProgress, VisitStatus.Completed, VisitStatus.Cancelled,
        }));
    }

    [Test]
    public async Task ApplyCommand_ChangedFields_AppliesFilterToAppState()
    {
        var viewModel = await CreateAndEnterAsync();
        viewModel.OnlyOverdue = false;
        viewModel.Status = VisitStatus.Completed;

        await viewModel.ApplyCommand.ExecuteAsync(null);

        _appState.Verify(a => a.ApplyFilter(false, VisitStatus.Completed), Times.Once);
    }

    [Test]
    public async Task ApplyCommand_Executed_SendsFilterChangedMessage()
    {
        var viewModel = await CreateAndEnterAsync();

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.That(_recipient.ReceivedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyCommand_Executed_ClosesModal()
    {
        var viewModel = await CreateAndEnterAsync();

        await viewModel.ApplyCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.CloseModalAsync(), Times.Once);
    }

    [Test]
    public async Task ApplyCommand_Executed_ApplyThenMessageThenClose()
    {
        // Kolejność z kontraktu: najpierw stan, potem powiadomienie, na końcu zamknięcie modala
        var viewModel = await CreateAndEnterAsync();

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.That(_events, Is.EqualTo(new[] { "apply", "message", "close" }));
    }

    [Test]
    public async Task ApplyCommand_Executed_DoesNotChangeFilterDirectly()
    {
        // Jedno źródło prawdy: zmiana filtra tylko przez appState.ApplyFilter
        var viewModel = await CreateAndEnterAsync();
        viewModel.OnlyOverdue = false;
        viewModel.Status = null;

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_filter.OnlyOverdue, Is.True);
            Assert.That(_filter.Status, Is.EqualTo(VisitStatus.Planned));
        });
    }

    [Test]
    public async Task ClearCommand_Executed_AppliesFilterWithoutRestrictions()
    {
        var viewModel = await CreateAndEnterAsync();

        await viewModel.ClearCommand.ExecuteAsync(null);

        _appState.Verify(a => a.ApplyFilter(false, null), Times.Once);
    }

    [Test]
    public async Task ClearCommand_Executed_SendsMessageAndClosesModal()
    {
        var viewModel = await CreateAndEnterAsync();

        await viewModel.ClearCommand.ExecuteAsync(null);

        Assert.That(_events, Is.EqualTo(new[] { "apply", "message", "close" }));
    }

    // Testowy odbiorca komunikatu – zapisuje odbiór do wspólnej listy zdarzeń
    internal sealed class TestRecipient(List<string> events) : IRecipient<FilterChangedMessage>
    {
        public int ReceivedCount { get; private set; }

        public void Receive(FilterChangedMessage message)
        {
            ReceivedCount++;
            events.Add("message");
        }
    }

    [Test]
    public async Task ChooseStatusCommand_OptionChosen_SetsStatus()
    {
        _feedback.Setup(f => f.ChooseAsync("Visits_ChooseStatus", It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync((string _, IReadOnlyList<string> options) => options[2]);
        var viewModel = new VisitFilterViewModel(_appState.Object, _navigation.Object, _feedback.Object, new TestLocalizer());

        await viewModel.ChooseStatusCommand.ExecuteAsync(null);

        Assert.That(viewModel.Status, Is.EqualTo(viewModel.Statuses[2]));
    }
}
