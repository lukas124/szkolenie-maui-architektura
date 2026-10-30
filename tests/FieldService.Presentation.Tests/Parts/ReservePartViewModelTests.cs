using FieldService.Presentation.Feedback;
using System.Globalization;
using FieldService.Application.Abstractions;
using FieldService.Application.Parts.Commands;
using FieldService.Application.Parts.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Presentation.Common;
using FieldService.Presentation.Navigation;
using FieldService.Presentation.Parts;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Parts;

[TestFixture]
public class ReservePartViewModelTests
{
    private static readonly Guid Visit = Guid.Parse("6f1c2a3b-4d5e-4f60-8a7b-9c0d1e2f3a4b");

    // Czas wygaśnięcia w strefie telefonu z atrapy (Europe/Warsaw, 30.10 obowiązuje +01:00) – wynik 14:05
    // nie zależy od strefy maszyny, na której działają testy
    private static readonly DateTimeOffset ExpiresAt = new(2026, 10, 30, 14, 5, 0, TimeSpan.FromHours(1));

    private static readonly CatalogPart Compressor = new("KOMPRESOR-R32", "Kompresor R32", 1899.5m);
    private static readonly CatalogPart Filter = new("FILTR-SIATKOWY-22", "Filtr siatkowy 22 mm", 49.99m);
    private static readonly Warehouse Central = new("MAG-01", "Magazyn Centralny");
    private static readonly Warehouse South = new("MAG-02", "Magazyn Południe");

    // Atrapa lokalizatora: zna tekst potwierdzenia rezerwacji i wzorzec godziny, inaczej zwraca klucz
    private sealed class FakeLocalizer : ILocalizer
    {
        public string this[string key] => key switch
        {
            "Parts_Reserved" => "Zarezerwowano {0} szt. do {1}",
            "Format_Time" => "H:mm",
            _ => key
        };

        public CultureInfo CurrentCulture { get; } = new("pl-PL");
        public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    }

    private Mock<IInventoryGateway> _inventory = null!;
    private Mock<IUserFeedback> _feedback = null!;
    private Mock<INavigationService> _navigation = null!;
    private ReservePartViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _inventory = new Mock<IInventoryGateway>();
        _inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Success(new[] { Filter, Compressor }));
        _inventory.Setup(i => i.GetWarehousesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Warehouse>>.Success(new[] { Central, South }));
        _feedback = new Mock<IUserFeedback>();
        _navigation = new Mock<INavigationService>();

        _viewModel = new ReservePartViewModel(
            new GetPartCatalogHandler(_inventory.Object),
            new GetWarehousesHandler(_inventory.Object),
            new ReservePartHandler(_inventory.Object),
            _feedback.Object,
            _navigation.Object,
            new FakeLocalizer());
    }

    private void ReserveReturns(Result<PartReservation> result) =>
        _inventory.Setup(i => i.ReserveAsync(It.IsAny<ReservePart>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

    // Po wejściu: katalog i magazyny załadowane
    [Test]
    public async Task NavigatedTo_LoadsPartsAndWarehouses()
    {
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));

        Assert.That(_viewModel.Parts, Is.EquivalentTo(new[] { Filter, Compressor }));
        Assert.That(_viewModel.Warehouses, Is.EqualTo(new[] { Central, South }));
    }

    // Po wejściu: pierwszy magazyn wybrany, część nie, ilość 1
    [Test]
    public async Task NavigatedTo_SelectsFirstWarehouse()
    {
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));

        Assert.That(_viewModel.SelectedWarehouse, Is.EqualTo(Central));
        Assert.That(_viewModel.SelectedPart, Is.Null);
        Assert.That(_viewModel.Quantity, Is.EqualTo(1));
    }

    // Błąd ładowania katalogu → komunikat
    [Test]
    public async Task NavigatedTo_CatalogError_ShowsError()
    {
        var error = new Error("Api.Network", ErrorType.Network);
        _inventory.Setup(i => i.GetPartCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CatalogPart>>.Failure(error));

        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));

        _feedback.Verify(f => f.ShowErrorAsync(error), Times.Once);
    }

    // Błąd ładowania magazynów → komunikat
    [Test]
    public async Task NavigatedTo_WarehousesError_ShowsError()
    {
        var error = new Error("Api.Unauthorized", ErrorType.Unauthorized);
        _inventory.Setup(i => i.GetWarehousesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Warehouse>>.Failure(error));

        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));

        _feedback.Verify(f => f.ShowErrorAsync(error), Times.Once);
    }

    // CanExecute: bez części – nie; z częścią i magazynem – tak
    [Test]
    public async Task ReserveCommand_CanExecute_RequiresPartAndWarehouse()
    {
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));

        Assert.That(_viewModel.ReserveCommand.CanExecute(null), Is.False);

        _viewModel.SelectedPart = Compressor;
        Assert.That(_viewModel.ReserveCommand.CanExecute(null), Is.True);

        _viewModel.SelectedWarehouse = null;
        Assert.That(_viewModel.ReserveCommand.CanExecute(null), Is.False);
    }

    // Zmiana wyboru odświeża CanExecute (zdarzenie CanExecuteChanged)
    [Test]
    public async Task ReserveCommand_SelectingPart_RaisesCanExecuteChanged()
    {
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));
        var raised = false;
        _viewModel.ReserveCommand.CanExecuteChanged += (_, _) => raised = true;

        _viewModel.SelectedPart = Compressor;

        Assert.That(raised, Is.True);
    }

    // Komenda: ReservePart(wizyta z trasy, wybrana część, wybrany magazyn, ilość)
    [Test]
    public async Task Reserve_SendsCommandWithSelection()
    {
        ReserveReturns(Result<PartReservation>.Success(new PartReservation(Guid.NewGuid(), "KOMPRESOR-R32", "MAG-02", 2, ExpiresAt)));
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));
        _viewModel.SelectedPart = Compressor;
        _viewModel.SelectedWarehouse = South;
        _viewModel.Quantity = 2;

        await _viewModel.ReserveCommand.ExecuteAsync(null);

        _inventory.Verify(i => i.ReserveAsync(new ReservePart(Visit, "KOMPRESOR-R32", "MAG-02", 2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Sukces → ResultText z zasobów: ilość i czas wygaśnięcia "H:mm"
    [Test]
    public async Task Reserve_Success_SetsResultText()
    {
        ReserveReturns(Result<PartReservation>.Success(new PartReservation(Guid.NewGuid(), "KOMPRESOR-R32", "MAG-01", 2, ExpiresAt)));
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));
        _viewModel.SelectedPart = Compressor;
        _viewModel.Quantity = 2;

        await _viewModel.ReserveCommand.ExecuteAsync(null);

        Assert.That(_viewModel.ResultText, Is.EqualTo("Zarezerwowano 2 szt. do 14:05"));
        _feedback.Verify(f => f.ShowErrorAsync(It.IsAny<Error>()), Times.Never);
    }

    // Po udanej rezerwacji przycisk nieaktywny do nowego wyboru – drugie dotknięcie nie rezerwuje drugiej sztuki
    [Test]
    public async Task Reserve_Success_DisablesReserveUntilNextChoice()
    {
        ReserveReturns(Result<PartReservation>.Success(new PartReservation(Guid.NewGuid(), "KOMPRESOR-R32", "MAG-01", 1, ExpiresAt)));
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));
        _viewModel.SelectedPart = Compressor;

        await _viewModel.ReserveCommand.ExecuteAsync(null);

        Assert.That(_viewModel.ReserveCommand.CanExecute(null), Is.False);
    }

    // Konflikt (ktoś był szybszy) → komunikat błędu, ResultText bez zmian
    [Test]
    public async Task Reserve_Conflict_ShowsErrorAndKeepsResultText()
    {
        ReserveReturns(Result<PartReservation>.Failure(InventoryErrors.OutOfStock));
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));
        _viewModel.SelectedPart = Compressor;

        await _viewModel.ReserveCommand.ExecuteAsync(null);

        _feedback.Verify(f => f.ShowErrorAsync(InventoryErrors.OutOfStock), Times.Once);
        Assert.That(_viewModel.ResultText, Is.Null);
    }

    // Brak sieci → komunikat błędu
    [Test]
    public async Task Reserve_Network_ShowsError()
    {
        var error = new Error("Api.Network", ErrorType.Network);
        ReserveReturns(Result<PartReservation>.Failure(error));
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));
        _viewModel.SelectedPart = Compressor;

        await _viewModel.ReserveCommand.ExecuteAsync(null);

        _feedback.Verify(f => f.ShowErrorAsync(error), Times.Once);
    }

    // Ilość 0 → błąd walidacji z handlera, bez wywołania bramy
    [Test]
    public async Task Reserve_ZeroQuantity_ShowsInvalidQuantity()
    {
        await _viewModel.OnNavigatedToAsync(new ReservePartRoute(Visit));
        _viewModel.SelectedPart = Compressor;
        _viewModel.Quantity = 0;

        await _viewModel.ReserveCommand.ExecuteAsync(null);

        _feedback.Verify(f => f.ShowErrorAsync(InventoryErrors.InvalidQuantity), Times.Once);
        _inventory.Verify(i => i.ReserveAsync(It.IsAny<ReservePart>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // CloseCommand → zamknięcie modala
    [Test]
    public async Task Close_ClosesModal()
    {
        await _viewModel.CloseCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.CloseModalAsync(), Times.Once);
    }
}
