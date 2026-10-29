namespace FieldService.UI.Dialogs;

// Wybór z listy jako strona modalna. Result kończy się wybraną opcją albo null (Anuluj, systemowe „wstecz”).
public partial class ChoicePage : ContentPage
{
    private readonly TaskCompletionSource<string?> _result = new();
    private bool _closing;

    public ChoicePage(string title, string cancel, IReadOnlyList<string> options)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        CancelButton.Text = cancel;
        foreach (var option in options)
        {
            var button = new Button { Text = option, LineBreakMode = LineBreakMode.WordWrap }; // długa nazwa zawija się, nie jest ucinana
            if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("SecondaryButton", out var style) == true)
                button.Style = (Style)style;
            button.Clicked += (_, _) => _ = CloseAsync(option);
            OptionsLayout.Children.Add(button);
        }
    }

    // wybrana opcja; null = anulowano
    public Task<string?> Result => _result.Task;

    private void OnCancelClicked(object? sender, EventArgs e) => _ = CloseAsync(null);

    // Android: systemowy przycisk „wstecz” zamyka wybór jak „Anuluj”
    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async Task CloseAsync(string? result)
    {
        if (_closing) return; // drugie dotknięcie w trakcie zamykania
        _closing = true;
        try
        {
            await Navigation.PopModalAsync(false);
        }
        finally
        {
            _result.TrySetResult(result);
        }
    }
}
