using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Parts;

namespace FieldService.Presentation.Tests.Parts;

public class VanStockViewModelTests
{
    private static readonly Error NetworkError = new("Inventory.Network", ErrorType.Network);

    private Mock<IInventoryGateway> _inventory = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private VanStockViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _inventory = new Mock<IInventoryGateway>();
        _feedback = new Mock<IUserFeedback>();
        _feedback.Setup(f => f.ShowErrorAsync(It.IsAny<Error>())).Returns(Task.CompletedTask);
        _viewModel = new VanStockViewModel(new GetVanStockHandler(_inventory.Object), _feedback.Object);
    }

    private void SetupStock(params VanStockItem[] items) =>
        _inventory.Setup(i => i.GetVanStockAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VanStockItem>>.Success(items));

    private void SetupFailure() =>
        _inventory.Setup(i => i.GetVanStockAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<VanStockItem>>.Failure(NetworkError));

    private static VanStockItem Filter => new("P-FILTR-01", "Filtr wody", 4);
    private static VanStockItem Seal => new("P-USZCZ-02", "Uszczelka 1/2\"", 12);
    private static VanStockItem Valve => new("P-ZAWOR-03", "Zawór bezpieczeństwa", 1);

    [Test]
    public void Constructor_BeforeNavigatedTo_PartsEmptyAndNoQuery()
    {
        Assert.That(_viewModel.Parts, Is.Empty);
        _inventory.Verify(i => i.GetVanStockAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task OnNavigatedToAsync_StockAvailable_LoadsParts()
    {
        SetupStock(Filter, Seal);

        await _viewModel.OnNavigatedToAsync(new VanStockRoute());

        Assert.That(_viewModel.Parts, Is.EquivalentTo(new[] { Filter, Seal }));
    }

    [Test]
    public async Task OnNavigatedToAsync_UnsortedStock_PartsSortedByName()
    {
        SetupStock(Valve, Filter, Seal);

        await _viewModel.OnNavigatedToAsync(new VanStockRoute());

        Assert.That(_viewModel.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Filtr wody", "Uszczelka 1/2\"", "Zawór bezpieczeństwa" }));
    }

    [Test]
    public async Task OnNavigatedToAsync_QueryFails_ShowsErrorAndPartsEmpty()
    {
        SetupFailure();

        await _viewModel.OnNavigatedToAsync(new VanStockRoute());

        _feedback.Verify(f => f.ShowErrorAsync(NetworkError), Times.Once);
        Assert.That(_viewModel.Parts, Is.Empty);
    }

    [Test]
    public async Task RefreshCommand_NewStock_ReplacesParts()
    {
        SetupStock(Filter, Seal);
        await _viewModel.OnNavigatedToAsync(new VanStockRoute());
        SetupStock(Valve);

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.That(_viewModel.Parts, Is.EqualTo(new[] { Valve }));
    }

    [Test]
    public async Task RefreshCommand_Executed_IsRefreshingFalse()
    {
        SetupStock(Filter);
        await _viewModel.OnNavigatedToAsync(new VanStockRoute());
        _viewModel.IsRefreshing = true;   // tak ustawia RefreshView przy przeciągnięciu

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.That(_viewModel.IsRefreshing, Is.False);
    }

    [Test]
    public async Task RefreshCommand_QueryFails_ShowsErrorAndPartsUnchanged()
    {
        SetupStock(Filter, Seal);
        await _viewModel.OnNavigatedToAsync(new VanStockRoute());
        SetupFailure();

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _feedback.Verify(f => f.ShowErrorAsync(NetworkError), Times.Once);
        Assert.That(_viewModel.Parts, Is.EqualTo(new[] { Filter, Seal }));
    }

    [Test]
    public async Task RefreshCommand_QueryFails_IsRefreshingFalse()
    {
        SetupStock(Filter);
        await _viewModel.OnNavigatedToAsync(new VanStockRoute());
        SetupFailure();
        _viewModel.IsRefreshing = true;

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.That(_viewModel.IsRefreshing, Is.False);
    }
}
