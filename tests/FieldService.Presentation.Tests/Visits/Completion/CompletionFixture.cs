using System.Globalization;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Application.Visits.Commands;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Protocols;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Tests.TestSupport;
using FieldService.Presentation.Visits.Completion;
using FieldService.TestDoubles;

namespace FieldService.Presentation.Tests.Visits.Completion;

// Kreator złożony z prawdziwych kroków i handlerów; porty (replika, bramy, UI) jako atrapy
internal sealed class CompletionFixture
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["Activity_DIAG"] = "Diagnostyka",
        ["Activity_NAPR"] = "Naprawa",
        ["Activity_CZYSZ"] = "Czyszczenie",
        ["Activity_TEST"] = "Test działania",
    };

    public static readonly string[] ActivityCatalog = ["Diagnostyka", "Naprawa", "Czyszczenie", "Test działania"];

    public InMemoryVisitRepository Repository { get; } = new();
    public FixedClock Clock { get; } = new(TestData.Start.AddHours(1));
    public Mock<IVisitReplica> Replica { get; } = new();
    public Mock<IVisitsGateway> VisitsGateway { get; } = new();
    public Mock<IVisitPhotoStore> Photos { get; } = new();
    public Mock<IInventoryGateway> Inventory { get; } = new();
    public Mock<IUserFeedback> Feedback { get; } = new();
    public Mock<INavigationService> Navigation { get; } = new();
    public Mock<ILocalizer> Localizer { get; } = new();

    public CompletionDraft Draft { get; } = new();
    public VisitId VisitId { get; private set; } = new(Guid.NewGuid());

    // To, co zwraca lokalna replika (odczyt szczegółów)
    public VisitDetails? CurrentDetails { get; set; }

    public ActivitiesStepViewModel Activities { get; }
    public PartsStepViewModel Parts { get; }
    public SignatureStepViewModel Signature { get; }
    public SummaryStepViewModel Summary { get; }
    public CompleteVisitFlowViewModel Flow { get; }

    public CompletionFixture()
    {
        Localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => Texts.GetValueOrDefault(key, key));
        Localizer.Setup(l => l.CurrentCulture).Returns(CultureInfo.GetCultureInfo("pl-PL"));

        Replica.Setup(r => r.GetDetailsAsync(It.IsAny<VisitId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CurrentDetails);
        VisitsGateway.Setup(g => g.GetVisitAsync(It.IsAny<VisitId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Fail<RemoteVisit>(TestData.NetworkError));

        IReadOnlyList<VanStockItem> van = [new("P-1", "Filtr", 5), new("P-2", "Uszczelka", 10)];
        Inventory.Setup(i => i.GetVanStockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TestData.Ok(van));
        IReadOnlyList<CatalogPart> catalog = [new("P-1", "Filtr", 25.50m), new("P-2", "Uszczelka", 4.00m)];
        Inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TestData.Ok(catalog));

        Navigation.Setup(n => n.CloseModalAsync()).Returns(Task.CompletedTask);
        Navigation.Setup(n => n.GoBackAsync()).Returns(Task.CompletedTask);

        Activities = new ActivitiesStepViewModel(Draft, new RecordActivityHandler(Repository, Clock), DetailsHandler(), Localizer.Object, Feedback.Object);
        Parts = new PartsStepViewModel(Draft, new UsePartHandler(Repository, Clock), new RemoveUsedPartHandler(Repository, Clock),
            new GetVanStockHandler(Inventory.Object), DetailsHandler(), Feedback.Object, Localizer.Object);
        Signature = new SignatureStepViewModel(Draft, new SignByCustomerHandler(Repository, Clock), DetailsHandler(), Feedback.Object, Localizer.Object);
        Summary = new SummaryStepViewModel(Draft, ProtocolHandler(), Feedback.Object, Localizer.Object);
        Flow = new CompleteVisitFlowViewModel(Draft, Activities, Parts, Signature, Summary, DetailsHandler(),
            new CompleteVisitHandler(Repository, Clock), Feedback.Object, Navigation.Object, Localizer.Object);
    }

    public GetVisitDetailsHandler DetailsHandler() => new(Replica.Object, VisitsGateway.Object, Photos.Object);

    public GetVisitProtocolHandler ProtocolHandler() => new(Repository, Inventory.Object, Clock);

    // Wizyta w toku w repozytorium (Id kreatora = Id tej wizyty)
    public void SeedInProgressVisit() => VisitId = TestData.SeedInProgress(Repository, TestData.Start);

    public async Task RecordActivityAsync(string code, string description)
    {
        var result = await new RecordActivityHandler(Repository, Clock)
            .HandleAsync(new RecordActivity(VisitId, code, description), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True, "przygotowanie: zapis czynności");
    }

    public async Task UsePartAsync(string partId, int quantity)
    {
        var result = await new UsePartHandler(Repository, Clock)
            .HandleAsync(new UsePart(VisitId, new PartId(partId), quantity), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True, "przygotowanie: zużycie części");
    }

    public async Task<VisitProtocol> ProtocolAsync()
    {
        var result = await ProtocolHandler().HandleAsync(new GetVisitProtocol(VisitId), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True, "odczyt protokołu");
        return result.Value;
    }

    // Czy wizyta jest wciąż w toku (kolejna czynność daje się zapisać – R2)
    public async Task<bool> IsStillInProgressAsync()
    {
        var result = await new RecordActivityHandler(Repository, Clock)
            .HandleAsync(new RecordActivity(VisitId, "TEST", "Test działania"), CancellationToken.None);
        return result.IsSuccess;
    }

    public VisitDetails Details(IReadOnlyList<string>? activities = null, IReadOnlyList<VisitPartLine>? parts = null,
        bool hasValidSignature = false, string customerName = "Jan Kowalski") =>
        new(VisitId.Value, "ZL-2026-001", VisitStatus.InProgress, TestData.Start, TestData.Start.AddHours(2),
            new CustomerSnapshot(customerName, "500100200", "Kraków, ul. Długa 1"), "Pompa ciepła", "Nie grzeje", "",
            activities ?? [], parts ?? [], hasValidSignature, hasValidSignature ? customerName : null, null, 0);

    // Szczegóły w replice i w stanie przepływu (jak po wejściu do kreatora)
    public void UseDetails(VisitDetails details)
    {
        CurrentDetails = details;
        Draft.VisitId = VisitId;
        Draft.Details = details;
    }

    public void VerifyErrorShown() =>
        Feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.AtLeastOnce);

    public void VerifyNoErrorShown() =>
        Feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
}
