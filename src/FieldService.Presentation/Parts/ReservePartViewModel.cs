using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Parts.Commands;
using FieldService.Application.Parts.Queries;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Parts;

// Rezerwacja części z magazynu (komenda do OHS Magazynu): wynik od razu – „zarezerwowano do 14:30” albo zrozumiały
// komunikat, gdy ktoś był szybszy. POST bez automatycznych ponowień – przy braku sieci technik ponawia sam.
public sealed partial class ReservePartViewModel(
    GetPartCatalogHandler catalog,
    GetWarehousesHandler warehouses,
    ReservePartHandler reserve,
    IUserFeedback feedback,
    INavigationService navigation,
    ILocalizer localizer) : BaseViewModel, INavigatedTo<ReservePartRoute>
{
    private Guid _visitId;

    public ObservableCollection<CatalogPart> Parts { get; } = [];

    public ObservableCollection<Warehouse> Warehouses { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReserveCommand))]
    [NotifyPropertyChangedFor(nameof(PartText))]
    public partial CatalogPart? SelectedPart { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReserveCommand))]
    [NotifyPropertyChangedFor(nameof(WarehouseText))]
    public partial Warehouse? SelectedWarehouse { get; set; }

    // teksty przycisków wyboru (wybór z listy – strona modalna przez IUserFeedback.ChooseAsync)
    public string PartText => SelectedPart?.Name ?? localizer["Parts_ChoosePart"];

    public string WarehouseText => SelectedWarehouse?.Name ?? localizer["Parts_ChooseWarehouse"];

    [ObservableProperty]
    public partial int Quantity { get; set; } = 1;

    // po udanej rezerwacji: „Zarezerwowano 1 szt. do 14:30”
    [ObservableProperty]
    public partial string? ResultText { get; set; }

    public async Task OnNavigatedToAsync(ReservePartRoute route)
    {
        _visitId = route.VisitId;
        var parts = await catalog.HandleAsync(new GetPartCatalog(), CancellationToken.None);
        if (!parts.IsSuccess)
        {
            await feedback.ShowErrorAsync(parts.Error!);
            return;
        }
        Parts.ReplaceWith(parts.Value);

        var places = await warehouses.HandleAsync(new GetWarehouses(), CancellationToken.None);
        if (!places.IsSuccess)
        {
            await feedback.ShowErrorAsync(places.Error!);
            return;
        }
        Warehouses.ReplaceWith(places.Value);
        SelectedWarehouse = Warehouses.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ChoosePartAsync()
    {
        var chosen = await feedback.ChooseAsync("Parts_ChoosePart", Parts.Select(p => p.Name).ToList());
        if (chosen is not null) SelectedPart = Parts.First(p => p.Name == chosen);
    }

    [RelayCommand]
    private async Task ChooseWarehouseAsync()
    {
        var chosen = await feedback.ChooseAsync("Parts_ChooseWarehouse", Warehouses.Select(w => w.Name).ToList());
        if (chosen is not null) SelectedWarehouse = Warehouses.First(w => w.Name == chosen);
    }

    private bool CanReserve() => SelectedPart is not null && SelectedWarehouse is not null;

    [RelayCommand(CanExecute = nameof(CanReserve))]
    private async Task ReserveAsync()
    {
        var command = new ReservePart(_visitId, SelectedPart!.PartId, SelectedWarehouse!.Id, Quantity);
        var result = await reserve.HandleAsync(command, CancellationToken.None);
        if (!result.IsSuccess)
        {
            // TODO: Ćwiczenie 13 – pokaż błąd przez feedback (tekst z Strings.resx: Error_{Code}, np. Error_Inventory.OutOfStock)
            return;
        }

        var expires = TimeZoneInfo.ConvertTime(result.Value.ExpiresAt, localizer.TimeZone)
            .ToString(localizer["Format_Time"], localizer.CurrentCulture);
        ResultText = string.Format(localizer.CurrentCulture, localizer["Parts_Reserved"], result.Value.Quantity, expires);
        SelectedPart = null; // kolejne dotknięcie „Zarezerwuj” nie zarezerwuje drugiej sztuki – najpierw nowy wybór
    }

    [RelayCommand]
    private Task CloseAsync() => navigation.CloseModalAsync();
}
