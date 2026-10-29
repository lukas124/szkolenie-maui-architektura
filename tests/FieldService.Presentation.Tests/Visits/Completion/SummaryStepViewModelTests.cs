using FieldService.Domain.Visits;

namespace FieldService.Presentation.Tests.Visits.Completion;

public class SummaryStepViewModelTests
{
    private CompletionFixture _fx = null!;

    [SetUp]
    public void SetUp()
    {
        _fx = new CompletionFixture();
        _fx.SeedInProgressVisit();
        _fx.UseDetails(_fx.Details());
    }

    [Test]
    public void IsValid_BeforeShown_IsFalse()
    {
        Assert.That(_fx.Summary.Protocol, Is.Null);
        Assert.That(_fx.Summary.IsValid, Is.False);
    }

    [Test]
    public async Task OnShownAsync_KnownVisit_LoadsProtocolAndIsValid()
    {
        await _fx.UsePartAsync("P-1", 2);

        await _fx.Summary.OnShownAsync();

        Assert.That(_fx.Summary.Protocol, Is.Not.Null);
        Assert.That(_fx.Summary.Protocol!.Parts.Single().Name, Is.EqualTo("Filtr"));
        Assert.That(_fx.Summary.Protocol.PartsTotal, Is.EqualTo(51.00m));
        Assert.That(_fx.Summary.IsValid, Is.True);
    }

    [Test]
    public async Task OnShownAsync_UnknownVisit_ShowsErrorAndStaysInvalid()
    {
        _fx.Draft.VisitId = new VisitId(Guid.NewGuid());

        await _fx.Summary.OnShownAsync();

        Assert.That(_fx.Summary.Protocol, Is.Null);
        Assert.That(_fx.Summary.IsValid, Is.False);
        _fx.VerifyErrorShown();
    }

    [Test]
    public void NextButtonText_LastStep_DiffersFromOtherSteps()
    {
        // Klucz tekstu „Zakończ wizytę” nie jest w kontrakcie – sprawdzamy tylko, że ostatni krok ma własny tekst
        Assert.That(_fx.Summary.NextButtonText, Is.Not.Empty);
        Assert.That(_fx.Summary.NextButtonText, Is.Not.EqualTo(_fx.Activities.NextButtonText));
    }
}
