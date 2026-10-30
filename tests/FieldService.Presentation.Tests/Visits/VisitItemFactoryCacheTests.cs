using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Visits;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Visits;

// VisitItemFactory: tekst daty liczony raz (FormatRelativeDate), miniatura przez IThumbnailCache
[TestFixture]
public sealed class VisitItemFactoryCacheTests
{
    private const string ThumbnailUrl = "https://api.test/api/images/abc?size=thumb";
    private static readonly DateTimeOffset PlannedStart = new(2026, 10, 25, 8, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset CompletedAt = new(2026, 10, 25, 9, 10, 0, TimeSpan.FromHours(1));

    private Mock<IVisitFormatter> _formatter = null!;
    private Mock<IThumbnailCache> _thumbnails = null!;
    private VisitItemFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _formatter = new Mock<IVisitFormatter>();
        _formatter.Setup(f => f.FormatRelativeDate(It.IsAny<DateTimeOffset>())).Returns("3 dni temu");
        _formatter.Setup(f => f.FormatOutcome(It.IsAny<VisitStatus>(), It.IsAny<VisitOutcomeType?>())).Returns("Naprawione");
        _thumbnails = new Mock<IThumbnailCache>();
        _factory = new VisitItemFactory(_formatter.Object, _thumbnails.Object);
    }

    private static VisitHistoryEntry Entry(DateTimeOffset? completedAt) =>
        new(Guid.Parse("3d0a6a52-5d4c-4c5a-8f5e-6a1c0e7b9a01"), "ZL-2610-1001", VisitStatus.Completed, PlannedStart,
            completedAt, "Jan Klient", "Piaseczno", "Pompa ciepła 8 kW", VisitOutcomeType.Fixed,
            ThumbnailUrl, "https://api.test/api/images/abc");

    // WhenText = FormatRelativeDate(CompletedAt), gdy wizyta zakończona
    [Test]
    public void CreateHistory_CompletedEntry_WhenTextFromCompletedAt()
    {
        var item = _factory.CreateHistory(Entry(CompletedAt), () => Task.CompletedTask);

        Assert.That(item.WhenText, Is.EqualTo("3 dni temu"));
        _formatter.Verify(f => f.FormatRelativeDate(CompletedAt), Times.Once);
    }

    // Brak CompletedAt (anulowana) → WhenText z PlannedStart
    [Test]
    public void CreateHistory_EntryWithoutCompletedAt_WhenTextFromPlannedStart()
    {
        _factory.CreateHistory(Entry(null), () => Task.CompletedTask);

        _formatter.Verify(f => f.FormatRelativeDate(PlannedStart), Times.Once);
    }

    // Tekst liczony raz w fabryce – wielokrotny odczyt WhenText nie formatuje ponownie
    [Test]
    public void CreateHistory_WhenTextReadManyTimes_FormatsOnce()
    {
        var item = _factory.CreateHistory(Entry(CompletedAt), () => Task.CompletedTask);

        for (var i = 0; i < 5; i++)
        {
            _ = item.WhenText;
        }

        _formatter.Verify(f => f.FormatRelativeDate(It.IsAny<DateTimeOffset>()), Times.Once);
    }

    // Miniatura w historii otwierana przez IThumbnailCache z ThumbnailUrl wpisu
    [Test]
    public async Task CreateHistory_ThumbnailInvoked_OpensFromThumbnailCache()
    {
        var png = new MemoryStream([1, 2, 3]);
        _thumbnails.Setup(t => t.OpenAsync(ThumbnailUrl, It.IsAny<CancellationToken>())).ReturnsAsync(png);
        var item = _factory.CreateHistory(Entry(CompletedAt), () => Task.CompletedTask);

        var stream = await item.Thumbnail(CancellationToken.None);

        Assert.That(stream, Is.SameAs(png));
        _thumbnails.Verify(t => t.OpenAsync(ThumbnailUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Miniatura nie jest pobierana przy tworzeniu elementu – dopiero gdy UI wywoła Thumbnail
    [Test]
    public void CreateHistory_NotInvoked_DoesNotOpenThumbnail()
    {
        _factory.CreateHistory(Entry(CompletedAt), () => Task.CompletedTask);

        _thumbnails.Verify(t => t.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Miniatura na liście wizyt (VisitItem) też idzie przez IThumbnailCache z ThumbnailUrl podsumowania
    [Test]
    public async Task Create_ThumbnailInvoked_OpensFromThumbnailCache()
    {
        var summary = TodayVisitsFixture.Summary(Guid.Parse("3d0a6a52-5d4c-4c5a-8f5e-6a1c0e7b9a02"),
            VisitStatus.Planned, PlannedStart);
        _thumbnails.Setup(t => t.OpenAsync(summary.ThumbnailUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stream.Null);
        var item = _factory.Create(summary);

        await item.Thumbnail(CancellationToken.None);

        _thumbnails.Verify(t => t.OpenAsync(summary.ThumbnailUrl, It.IsAny<CancellationToken>()), Times.Once);
    }
}
