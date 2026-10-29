using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;

namespace FieldService.Presentation.Tests.Visits.Completion;

public class SignatureStepViewModelTests
{
    private CompletionFixture _fx = null!;

    [SetUp]
    public void SetUp()
    {
        _fx = new CompletionFixture();
        _fx.SeedInProgressVisit();
    }

    // Krok po wejściu: zakres z jedną czynnością, imię klienta podpowiedziane
    private async Task ShowWithActivityAsync()
    {
        _fx.UseDetails(_fx.Details(activities: ["Naprawa"]));
        await _fx.Signature.OnShownAsync();
    }

    [Test]
    public async Task OnShownAsync_Details_FillsSignerNameWithCustomerName()
    {
        await ShowWithActivityAsync();

        Assert.That(_fx.Signature.SignerName, Is.EqualTo("Jan Kowalski"));
    }

    [Test]
    public async Task IsValid_NameDrawingAndScope_IsTrue()
    {
        await ShowWithActivityAsync();

        _fx.Signature.HasDrawing = true;

        Assert.That(_fx.Signature.IsValid, Is.True);
    }

    [Test]
    public async Task IsValid_NoDrawing_IsFalse()
    {
        await ShowWithActivityAsync();

        _fx.Signature.HasDrawing = false;

        Assert.That(_fx.Signature.IsValid, Is.False);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task IsValid_BlankSignerName_IsFalse(string name)
    {
        await ShowWithActivityAsync();
        _fx.Signature.HasDrawing = true;

        _fx.Signature.SignerName = name;

        Assert.That(_fx.Signature.IsValid, Is.False);
    }

    [Test]
    public async Task IsValid_EmptyScope_IsFalse()
    {
        _fx.UseDetails(_fx.Details());
        await _fx.Signature.OnShownAsync();

        _fx.Signature.HasDrawing = true;

        Assert.That(_fx.Signature.IsValid, Is.False);
    }

    [Test]
    public async Task IsValid_OnlyPartsInScope_IsTrue()
    {
        _fx.UseDetails(_fx.Details(parts: [new VisitPartLine("P-1", 1)]));
        await _fx.Signature.OnShownAsync();

        _fx.Signature.HasDrawing = true;

        Assert.That(_fx.Signature.IsValid, Is.True);
    }

    [Test]
    public async Task IsValid_DrawingAdded_RaisesPropertyChanged()
    {
        await ShowWithActivityAsync();
        var changed = new List<string?>();
        _fx.Signature.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _fx.Signature.HasDrawing = true;

        // „Dalej” w kreatorze musi się odświeżyć
        Assert.That(changed, Does.Contain(nameof(_fx.Signature.IsValid)));
    }

    [Test]
    public async Task TryLeaveAsync_SignSucceeds_ReturnsTrueAndRefreshesDraft()
    {
        await _fx.RecordActivityAsync("NAPR", "Naprawa");
        await ShowWithActivityAsync();
        _fx.Signature.HasDrawing = true;
        var signed = _fx.Details(activities: ["Naprawa"], hasValidSignature: true);
        _fx.CurrentDetails = signed;

        var canLeave = await _fx.Signature.TryLeaveAsync();

        Assert.That(canLeave, Is.True);
        Assert.That(_fx.Draft.Details, Is.EqualTo(signed));
        // Podpis zapisany: zakończenie „naprawione” przechodzi (R5)
        var complete = await new CompleteVisitHandler(_fx.Repository, _fx.Clock)
            .HandleAsync(new CompleteVisit(_fx.VisitId, VisitOutcomeType.Fixed, null), CancellationToken.None);
        Assert.That(complete.IsSuccess, Is.True);
        _fx.VerifyNoErrorShown();
    }

    [Test]
    public async Task TryLeaveAsync_EmptyScopeInRepository_ShowsErrorAndReturnsFalse()
    {
        // R4: w repozytorium zakres pusty – podpis odrzucony
        await ShowWithActivityAsync();
        _fx.Signature.HasDrawing = true;

        var canLeave = await _fx.Signature.TryLeaveAsync();

        Assert.That(canLeave, Is.False);
        _fx.VerifyErrorShown();
    }

    [Test]
    public async Task TryLeaveAsync_UnknownVisit_ShowsErrorAndReturnsFalse()
    {
        await ShowWithActivityAsync();
        _fx.Signature.HasDrawing = true;
        _fx.Draft.VisitId = new VisitId(Guid.NewGuid());

        var canLeave = await _fx.Signature.TryLeaveAsync();

        Assert.That(canLeave, Is.False);
        _fx.VerifyErrorShown();
    }
}
