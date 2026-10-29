using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Parts.Queries;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Parts;

// „Auto technika”: części, które technik ma przy sobie (model Magazyn – tylko odczyt)
public sealed partial class VanStockViewModel(GetVanStockHandler vanStock, IUserFeedback feedback)
    : BaseViewModel, INavigatedTo<VanStockRoute>
{
    public ObservableCollection<VanStockItem> Parts { get; } = [];

    // gest „pociągnij, aby odświeżyć” (RefreshView sam ustawia true)
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    public Task OnNavigatedToAsync(VanStockRoute route) => LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try { await LoadAsync(); }
        finally { IsRefreshing = false; }
    }

    private async Task LoadAsync()
    {
        var result = await vanStock.HandleAsync(new GetVanStock(), CancellationToken.None);
        if (!result.IsSuccess)
        {
            await feedback.ShowErrorAsync(result.Error!);
            return;
        }

        Parts.ReplaceWith(result.Value.OrderBy(p => p.Name));
    }
}
