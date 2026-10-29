using FieldService.Presentation.Parts;

namespace FieldService.UI.Parts.Pages;

public partial class VanStockPage : ContentPage
{
    private readonly VanStockViewModel _viewModel;

    public VanStockPage(VanStockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnRefreshClicked(object sender, EventArgs e) =>
        await _viewModel.RefreshCommand.ExecuteAsync(null);
}
