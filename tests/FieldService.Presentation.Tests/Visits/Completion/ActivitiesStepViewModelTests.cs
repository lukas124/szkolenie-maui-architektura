using FieldService.Domain.Visits;

namespace FieldService.Presentation.Tests.Visits.Completion;

public class ActivitiesStepViewModelTests
{
    private CompletionFixture _fx = null!;

    [SetUp]
    public void SetUp()
    {
        _fx = new CompletionFixture();
        _fx.SeedInProgressVisit();
    }

    private void ChooseActivity(string? choice) =>
        _fx.Feedback.Setup(f => f.ChooseAsync("Completion_ChooseActivity", It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(choice);

    [Test]
    public async Task IsValid_EmptyScope_IsTrue()
    {
        _fx.UseDetails(_fx.Details());

        await _fx.Activities.OnShownAsync();

        // Zakres może mieć same części
        Assert.That(_fx.Activities.IsValid, Is.True);
    }

    [Test]
    public async Task OnShownAsync_DetailsWithActivities_ShowsDescriptions()
    {
        _fx.UseDetails(_fx.Details(activities: ["Diagnostyka", "Naprawa"]));

        await _fx.Activities.OnShownAsync();

        Assert.That(_fx.Activities.Activities, Is.EqualTo(new[] { "Diagnostyka", "Naprawa" }));
    }

    [Test]
    public async Task AddActivity_Always_OffersLocalizedCatalog()
    {
        _fx.UseDetails(_fx.Details());
        ChooseActivity(null);

        await _fx.Activities.AddActivityCommand.ExecuteAsync(null);

        _fx.Feedback.Verify(f => f.ChooseAsync("Completion_ChooseActivity",
            It.Is<IReadOnlyList<string>>(o => o.SequenceEqual(CompletionFixture.ActivityCatalog))), Times.Once);
    }

    [Test]
    public async Task AddActivity_Chosen_RecordsActivityAndRefreshesDraft()
    {
        _fx.UseDetails(_fx.Details());
        await _fx.Activities.OnShownAsync();
        ChooseActivity("Naprawa");
        var refreshed = _fx.Details(activities: ["Naprawa"]);
        _fx.CurrentDetails = refreshed;

        await _fx.Activities.AddActivityCommand.ExecuteAsync(null);

        var protocol = await _fx.ProtocolAsync();
        Assert.That(protocol.Activities.Any(a => a.Contains("Naprawa")), Is.True);
        Assert.That(_fx.Draft.Details, Is.EqualTo(refreshed));
        Assert.That(_fx.Activities.Activities, Is.EqualTo(new[] { "Naprawa" }));
    }

    [Test]
    public async Task AddActivity_Cancelled_RecordsNothing()
    {
        _fx.UseDetails(_fx.Details());
        ChooseActivity(null);

        await _fx.Activities.AddActivityCommand.ExecuteAsync(null);

        var protocol = await _fx.ProtocolAsync();
        Assert.That(protocol.Activities, Is.Empty);
        _fx.VerifyNoErrorShown();
    }

    [Test]
    public async Task AddActivity_RecordFails_ShowsError()
    {
        // Wizyta spoza repozytorium – zapis czynności kończy się błędem
        _fx.UseDetails(_fx.Details());
        _fx.Draft.VisitId = new VisitId(Guid.NewGuid());
        ChooseActivity("Naprawa");

        await _fx.Activities.AddActivityCommand.ExecuteAsync(null);

        _fx.VerifyErrorShown();
    }
}
