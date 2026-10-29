namespace FieldService.Presentation.Tests.Visits.Completion;

// R3 w kreatorze: po powrocie do kroku podpisu (np. po dopisaniu części) pole podpisu jest puste –
// stary rysunek nie może podpisać nowego zakresu
[TestFixture]
public class SignatureStepR3Tests
{
    private CompletionFixture _fx = null!;

    [SetUp]
    public void SetUp()
    {
        _fx = new CompletionFixture();
        _fx.SeedInProgressVisit();
        _fx.UseDetails(_fx.Details(activities: ["Naprawa"]));
    }

    [Test]
    public async Task OnShownAsync_AgainAfterDrawing_RequiresNewDrawing()
    {
        await _fx.Signature.OnShownAsync();
        _fx.Signature.HasDrawing = true;

        await _fx.Signature.OnShownAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_fx.Signature.HasDrawing, Is.False);
            Assert.That(_fx.Signature.IsValid, Is.False);
        });
    }
}
