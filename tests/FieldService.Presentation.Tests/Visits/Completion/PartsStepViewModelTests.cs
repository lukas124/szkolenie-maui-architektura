using FieldService.Application.Visits.Queries;
using FieldService.Domain.Inventory;

namespace FieldService.Presentation.Tests.Visits.Completion;

public class PartsStepViewModelTests
{
    private CompletionFixture _fx = null!;

    [SetUp]
    public void SetUp()
    {
        _fx = new CompletionFixture();
        _fx.SeedInProgressVisit();
    }

    private void ChoosePart(string? choice) =>
        _fx.Feedback.Setup(f => f.ChooseAsync("Visit_ChoosePart", It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(choice);

    [Test]
    public async Task IsValid_EmptyScope_IsTrue()
    {
        _fx.UseDetails(_fx.Details());

        await _fx.Parts.OnShownAsync();

        Assert.That(_fx.Parts.IsValid, Is.True);
    }

    [Test]
    public async Task OnShownAsync_DetailsWithParts_ShowsParts()
    {
        var line = new VisitPartLine("P-1", 2);
        _fx.UseDetails(_fx.Details(parts: [line]));

        await _fx.Parts.OnShownAsync();

        Assert.That(_fx.Parts.Parts, Is.EqualTo(new[] { line }));
    }

    [Test]
    public async Task AddPart_Always_OffersVanStockNames()
    {
        _fx.UseDetails(_fx.Details());
        await _fx.Parts.OnShownAsync();
        ChoosePart(null);

        await _fx.Parts.AddPartCommand.ExecuteAsync(null);

        _fx.Feedback.Verify(f => f.ChooseAsync("Visit_ChoosePart",
            It.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { "Filtr", "Uszczelka" }))), Times.Once);
    }

    [Test]
    public async Task AddPart_Chosen_UsesOnePiece()
    {
        _fx.UseDetails(_fx.Details());
        await _fx.Parts.OnShownAsync();
        ChoosePart("Filtr");
        var refreshed = _fx.Details(parts: [new VisitPartLine("P-1", 1)]);
        _fx.CurrentDetails = refreshed;

        await _fx.Parts.AddPartCommand.ExecuteAsync(null);

        var protocol = await _fx.ProtocolAsync();
        var line = protocol.Parts.Single();
        Assert.That(line.PartId, Is.EqualTo(new PartId("P-1")));
        Assert.That(line.Quantity, Is.EqualTo(1));
        Assert.That(_fx.Draft.Details, Is.EqualTo(refreshed));
    }

    [Test]
    public async Task AddPart_Cancelled_UsesNothing()
    {
        _fx.UseDetails(_fx.Details());
        await _fx.Parts.OnShownAsync();
        ChoosePart(null);

        await _fx.Parts.AddPartCommand.ExecuteAsync(null);

        var protocol = await _fx.ProtocolAsync();
        Assert.That(protocol.Parts, Is.Empty);
    }

    [Test]
    public async Task RemovePart_PartInScope_RemovesPart()
    {
        await _fx.UsePartAsync("P-1", 1);
        var line = new VisitPartLine("P-1", 1);
        _fx.UseDetails(_fx.Details(parts: [line]));
        _fx.CurrentDetails = _fx.Details();

        await _fx.Parts.RemovePartCommand.ExecuteAsync(line);

        var protocol = await _fx.ProtocolAsync();
        Assert.That(protocol.Parts, Is.Empty);
        Assert.That(_fx.Parts.Parts, Is.Empty);
    }

    [Test]
    public async Task RemovePart_PartNotInScope_ShowsError()
    {
        // R7: części nie ma w zakresie
        _fx.UseDetails(_fx.Details());

        await _fx.Parts.RemovePartCommand.ExecuteAsync(new VisitPartLine("P-2", 1));

        _fx.VerifyErrorShown();
    }
}
