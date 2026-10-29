using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using static FieldService.Application.Tests.BlackBox.SpecVisitData;

namespace FieldService.Application.Tests.BlackBox;

/// <summary>
/// Przypadek użycia „wizyty na dziś”: sortowanie po początku okna, wyliczenie spóźnienia
/// (zaplanowana i okno już się zaczęło), przekazanie błędu bramy, bez zmian innych pól.
/// </summary>
[TestFixture]
public class GetTodayVisitsHandlerSpecTests
{
    // „Teraz” technika: czwartek 12.11.2026, 10:00 czasu środkowoeuropejskiego.
    private static readonly DateTimeOffset Now = At(12, 10);

    private Mock<IVisitsGateway> _gateway = null!;
    private GetTodayVisitsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IVisitsGateway>();
        _handler = new GetTodayVisitsHandler(_gateway.Object, EmptyReplica(), Mock.Of<IClock>(c => c.Now == Now));
    }

    // 07: wizyty zmienione w telefonie – tu żadnych
    private static IVisitReplica EmptyReplica() =>
        Mock.Of<IVisitReplica>(r => r.GetLocallyChangedStatusesAsync(It.IsAny<CancellationToken>())
            == Task.FromResult<IReadOnlyDictionary<Guid, VisitStatus>>(new Dictionary<Guid, VisitStatus>()));

    private void GatewayReturns(params VisitSummary[] visits) =>
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Success(visits));

    private Task<Result<IReadOnlyList<VisitSummary>>> Handle() =>
        _handler.HandleAsync(new GetTodayVisits(), CancellationToken.None);

    [Test]
    public async Task Sortuje_WizytyPoPoczatkuOkna()
    {
        var late = NewVisit(At(12, 14), customer: "C");
        var early = NewVisit(At(12, 8), status: VisitStatus.Completed, customer: "A");
        var middle = NewVisit(At(12, 10, 30), customer: "B");
        GatewayReturns(late, early, middle);

        var result = await Handle();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(v => v.CustomerName), Is.EqualTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public async Task Sortuje_PoChwiliBezwzglednej_GdyOffsetyRozne()
    {
        // 08:30Z = 9:30 CET – wcześniej niż 10:00 CET, mimo „większej” godziny w zapisie 10:00+01:00.
        var cet = NewVisit(At(12, 10), customer: "Później");
        var utc = NewVisit(new DateTimeOffset(2026, 11, 12, 8, 30, 0, TimeSpan.Zero), customer: "Wcześniej");
        GatewayReturns(cet, utc);

        var result = await Handle();

        Assert.That(result.Value.Select(v => v.CustomerName), Is.EqualTo(new[] { "Wcześniej", "Później" }));
    }

    [Test]
    public async Task Zaplanowana_KtorejOknoJuzSieZaczelo_JestSpozniona()
    {
        GatewayReturns(NewVisit(At(12, 9), duration: TimeSpan.FromHours(2)));

        var result = await Handle();

        Assert.That(result.Value[0].IsLate, Is.True);
    }

    [Test]
    public async Task Zaplanowana_KtorejOknoJuzSieSkonczylo_JestSpozniona()
    {
        GatewayReturns(NewVisit(At(12, 7), duration: TimeSpan.FromHours(1)));

        var result = await Handle();

        Assert.That(result.Value[0].IsLate, Is.True);
    }

    [Test]
    public async Task Zaplanowana_ZOknemWPrzyszlosci_NieJestSpozniona()
    {
        GatewayReturns(NewVisit(At(12, 11)));

        var result = await Handle();

        Assert.That(result.Value[0].IsLate, Is.False);
    }

    [Test]
    public async Task Spoznienie_LiczoneDlaCzasuWInnymOffsecie()
    {
        // 08:45Z = 9:45 CET, czyli przed „teraz” (10:00 CET).
        GatewayReturns(NewVisit(new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero)));

        var result = await Handle();

        Assert.That(result.Value[0].IsLate, Is.True);
    }

    [TestCase(VisitStatus.InProgress)]
    [TestCase(VisitStatus.Completed)]
    [TestCase(VisitStatus.Cancelled)]
    public async Task NiezaplanowanaZOknemWPrzeszlosci_NieJestSpozniona(VisitStatus status)
    {
        GatewayReturns(NewVisit(At(12, 8), status: status));

        var result = await Handle();

        Assert.That(result.Value[0].IsLate, Is.False);
    }

    [Test]
    public async Task NieZmieniaInnychPol()
    {
        var planned = NewVisit(At(12, 9));
        var future = NewVisit(At(12, 15));
        var completed = NewVisit(At(12, 8), status: VisitStatus.Completed);
        GatewayReturns(future, planned, completed);

        var result = await Handle();

        Assert.That(result.Value, Is.EqualTo(new[]
        {
            completed,
            planned with { IsLate = true },
            future,
        }));
    }

    [Test]
    public async Task PustaLista_ToSukcesZPustaLista()
    {
        GatewayReturns();

        var result = await Handle();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [TestCase(ErrorType.Network)]
    [TestCase(ErrorType.Unauthorized)]
    [TestCase(ErrorType.Unexpected)]
    public async Task BladBramy_JestPrzekazanyBezZmian(ErrorType type)
    {
        var error = new Error("Visits.Load", type);
        _gateway.Setup(g => g.GetTodayAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VisitSummary>>.Failure(error));

        var result = await Handle();

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(error));
    }

    [Test]
    public async Task PytaBrameDokladnieRaz()
    {
        GatewayReturns(NewVisit(At(12, 11)));

        await Handle();

        _gateway.Verify(g => g.GetTodayAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
