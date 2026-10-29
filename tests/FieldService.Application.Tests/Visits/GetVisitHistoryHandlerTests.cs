using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Tests.Visits;

public class GetVisitHistoryHandlerTests
{
    private static readonly DateTimeOffset Planned = new(2026, 10, 28, 8, 0, 0, TimeSpan.Zero);

    private Mock<IVisitsGateway> _gateway = null!;
    private GetVisitHistoryHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IVisitsGateway>();
        _handler = new GetVisitHistoryHandler(_gateway.Object);
    }

    private static VisitHistoryEntry Entry(string number) =>
        new(Guid.NewGuid(), number, VisitStatus.Completed, Planned, Planned.AddHours(1), "Jan Klient", "Piaseczno",
            "Pompa ciepła 8 kW", VisitOutcomeType.Fixed, "https://api.test/api/images/abc?size=thumb",
            "https://api.test/api/images/abc");

    [Test]
    public async Task HandleAsync_PageAndPageSize_PassesThemToGateway()
    {
        _gateway.Setup(g => g.GetHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Success(new VisitHistoryPageResult([], 3, 0)));

        await _handler.HandleAsync(new GetVisitHistory(3, 10), CancellationToken.None);

        _gateway.Verify(g => g.GetHistoryAsync(3, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_DefaultPageSize_Passes30()
    {
        _gateway.Setup(g => g.GetHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Success(new VisitHistoryPageResult([], 1, 0)));

        await _handler.HandleAsync(new GetVisitHistory(1), CancellationToken.None);

        _gateway.Verify(g => g.GetHistoryAsync(1, 30, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_GatewaySucceeds_ReturnsItemsPageAndTotalCount()
    {
        var first = Entry("ZL-2610-1001");
        var second = Entry("ZL-2610-1002");
        _gateway.Setup(g => g.GetHistoryAsync(2, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Success(new VisitHistoryPageResult([first, second], 2, 65)));

        var result = await _handler.HandleAsync(new GetVisitHistory(2, 30), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Items, Is.EqualTo(new[] { first, second }));
        Assert.That(result.Value.Page, Is.EqualTo(2));
        Assert.That(result.Value.TotalCount, Is.EqualTo(65));
    }

    [Test]
    public async Task HandleAsync_GatewayFails_ReturnsSameError()
    {
        var error = new Error("Visits.Network", ErrorType.Network);
        _gateway.Setup(g => g.GetHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<VisitHistoryPageResult>.Failure(error));

        var result = await _handler.HandleAsync(new GetVisitHistory(1), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(error));
    }
}
