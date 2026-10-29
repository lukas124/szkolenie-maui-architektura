using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Visits.Completion;

namespace FieldService.Presentation.Tests.Visits.Completion;

public class CompleteVisitFlowViewModelTests
{
    private CompletionFixture _fx = null!;

    [SetUp]
    public async Task SetUp()
    {
        _fx = new CompletionFixture();
        _fx.SeedInProgressVisit();
        // W repozytorium i w replice ta sama czynność – podpis jest możliwy
        await _fx.RecordActivityAsync("NAPR", "Naprawa");
        _fx.CurrentDetails = _fx.Details(activities: ["Naprawa"]);
    }

    private CompleteVisitFlowViewModel Flow => _fx.Flow;

    private Task OpenAsync() => Flow.OnNavigatedToAsync(new CompleteVisitRoute(_fx.VisitId));

    // Od wejścia do kroku podpisu z narysowanym podpisem
    private async Task GoToSignatureAsync()
    {
        await OpenAsync();
        await Flow.NextCommand.ExecuteAsync(null);
        await Flow.NextCommand.ExecuteAsync(null);
        _fx.Signature.HasDrawing = true;
    }

    private async Task GoToSummaryAsync()
    {
        await GoToSignatureAsync();
        await Flow.NextCommand.ExecuteAsync(null);
    }

    [Test]
    public void Steps_Always_ActivitiesPartsSignatureSummary()
    {
        Assert.That(Flow.Steps, Is.EqualTo(new CompletionStep[] { _fx.Activities, _fx.Parts, _fx.Signature, _fx.Summary }));
    }

    [Test]
    public async Task OnNavigatedToAsync_Route_FillsDraftAndShowsFirstStep()
    {
        await OpenAsync();

        Assert.That(_fx.Draft.VisitId, Is.EqualTo(_fx.VisitId));
        Assert.That(_fx.Draft.Details, Is.EqualTo(_fx.CurrentDetails));
        Assert.That(Flow.CurrentIndex, Is.EqualTo(0));
        Assert.That(Flow.CurrentStep, Is.SameAs(_fx.Activities));
        Assert.That(Flow.CanGoBack, Is.False);
        // Pierwszy krok dostał OnShownAsync
        Assert.That(_fx.Activities.Activities, Is.EqualTo(new[] { "Naprawa" }));
    }

    [Test]
    public async Task OnNavigatedToAsync_DetailsUnavailable_ShowsError()
    {
        // Brak w replice, brama zwraca błąd
        _fx.CurrentDetails = null;

        await OpenAsync();

        _fx.VerifyErrorShown();
    }

    [Test]
    public async Task NextCommand_ValidStep_MovesToNextStep()
    {
        await OpenAsync();

        await Flow.NextCommand.ExecuteAsync(null);

        Assert.That(Flow.CurrentIndex, Is.EqualTo(1));
        Assert.That(Flow.CurrentStep, Is.SameAs(_fx.Parts));
        Assert.That(Flow.CanGoBack, Is.True);
    }

    [Test]
    public async Task NextCommand_InvalidStep_CannotExecute()
    {
        await GoToSignatureAsync();

        _fx.Signature.HasDrawing = false;

        Assert.That(Flow.CurrentStep, Is.SameAs(_fx.Signature));
        Assert.That(Flow.NextCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task NextCommand_StepBecomesValid_CanExecute()
    {
        await GoToSignatureAsync();
        _fx.Signature.HasDrawing = false;

        _fx.Signature.HasDrawing = true;

        Assert.That(Flow.NextCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task NextCommand_TryLeaveFails_StaysOnStep()
    {
        await GoToSignatureAsync();
        // Wizyta spoza repozytorium – podpis się nie zapisze
        _fx.Draft.VisitId = new VisitId(Guid.NewGuid());

        await Flow.NextCommand.ExecuteAsync(null);

        Assert.That(Flow.CurrentIndex, Is.EqualTo(2));
        Assert.That(Flow.CurrentStep, Is.SameAs(_fx.Signature));
        _fx.VerifyErrorShown();
    }

    [Test]
    public async Task NextCommand_SignatureStep_ShowsSummaryWithProtocol()
    {
        await GoToSummaryAsync();

        Assert.That(Flow.CurrentIndex, Is.EqualTo(3));
        Assert.That(Flow.CurrentStep, Is.SameAs(_fx.Summary));
        Assert.That(_fx.Summary.Protocol, Is.Not.Null);
    }

    [Test]
    public async Task BackCommand_SecondStep_ReturnsToFirstStep()
    {
        await OpenAsync();
        await Flow.NextCommand.ExecuteAsync(null);

        Flow.BackCommand.Execute(null);

        Assert.That(Flow.CurrentIndex, Is.EqualTo(0));
        Assert.That(Flow.CurrentStep, Is.SameAs(_fx.Activities));
        Assert.That(Flow.CanGoBack, Is.False);
    }

    [Test]
    public async Task BackCommand_FromSummary_DoesNotSignAgain()
    {
        await GoToSummaryAsync();

        Flow.BackCommand.Execute(null);

        // Powrót bez TryLeaveAsync – brak błędów i jesteśmy na podpisie
        Assert.That(Flow.CurrentStep, Is.SameAs(_fx.Signature));
        _fx.VerifyNoErrorShown();
    }

    [Test]
    public async Task NextCommand_LastStep_CompletesThenClosesModalAndGoesBack()
    {
        var calls = new List<string>();
        _fx.Navigation.Setup(n => n.CloseModalAsync()).Callback(() => calls.Add("close")).Returns(Task.CompletedTask);
        _fx.Navigation.Setup(n => n.GoBackAsync()).Callback(() => calls.Add("back")).Returns(Task.CompletedTask);
        await GoToSummaryAsync();

        await Flow.NextCommand.ExecuteAsync(null);

        Assert.That(calls, Is.EqualTo(new[] { "close", "back" }));
        // Wizyta zakończona – zakres nie daje się już zmienić
        Assert.That(await _fx.IsStillInProgressAsync(), Is.False);
        _fx.VerifyNoErrorShown();
    }

    [Test]
    public async Task NextCommand_CompleteFails_ShowsErrorAndStaysInWizard()
    {
        await GoToSummaryAsync();
        // R3: zmiana zakresu po podpisie unieważnia podpis, więc „naprawione” odpadnie (R5)
        await _fx.RecordActivityAsync("CZYSZ", "Czyszczenie");

        await Flow.NextCommand.ExecuteAsync(null);

        _fx.Feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Once);
        _fx.Navigation.Verify(n => n.CloseModalAsync(), Times.Never);
        _fx.Navigation.Verify(n => n.GoBackAsync(), Times.Never);
        Assert.That(Flow.CurrentStep, Is.SameAs(_fx.Summary));
    }

    [Test]
    public async Task CancelCommand_AnyStep_ClosesModalWithoutCompleting()
    {
        await GoToSignatureAsync();

        await Flow.CancelCommand.ExecuteAsync(null);

        _fx.Navigation.Verify(n => n.CloseModalAsync(), Times.Once);
        _fx.Navigation.Verify(n => n.GoBackAsync(), Times.Never);
        Assert.That(await _fx.IsStillInProgressAsync(), Is.True);
    }
}
