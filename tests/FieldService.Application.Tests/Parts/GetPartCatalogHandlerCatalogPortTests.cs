using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common; // ZAŁOŻENIE: Result<T>, Error, ErrorType w tej przestrzeni nazw
using Moq;
using NUnit.Framework;

namespace FieldService.Application.Tests.Parts;

[TestFixture]
public sealed class GetPartCatalogHandlerCatalogPortTests
{
    private Mock<IPartsCatalog> _catalog = null!;
    private GetPartCatalogHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _catalog = new Mock<IPartsCatalog>();
        _handler = new GetPartCatalogHandler(_catalog.Object);
    }

    // Katalog z IPartsCatalog posortowany po nazwie
    [Test]
    public async Task HandleAsync_CatalogAvailable_ReturnsPartsSortedByName()
    {
        IReadOnlyList<CatalogPart> parts =
        [
            new("P-ZAWOR-03", "Zawór trójdrogowy", 210m),
            new("P-FILTR-01", "Filtr siatkowy", 25m),
            new("P-POMPA-02", "Pompa obiegowa", 480m),
        ];
        _catalog.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Success(parts));

        var result = await _handler.HandleAsync(new GetPartCatalog(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(p => p.Name),
            Is.EqualTo(new[] { "Filtr siatkowy", "Pompa obiegowa", "Zawór trójdrogowy" }));
    }

    // Błąd katalogu przechodzi bez zmian
    [Test]
    public async Task HandleAsync_CatalogFails_ReturnsSameError()
    {
        var network = new Error("Api.Network", ErrorType.Network);
        _catalog.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Failure(network));

        var result = await _handler.HandleAsync(new GetPartCatalog(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(network));
    }

    // Każde zapytanie sięga do IPartsCatalog (cache jest za portem, nie w handlerze)
    [Test]
    public async Task HandleAsync_CalledTwice_AsksCatalogTwice()
    {
        _catalog.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Success([]));

        await _handler.HandleAsync(new GetPartCatalog(), CancellationToken.None);
        await _handler.HandleAsync(new GetPartCatalog(), CancellationToken.None);

        _catalog.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
