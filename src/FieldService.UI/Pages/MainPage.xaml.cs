using Microsoft.Extensions.Configuration;

namespace FieldService.UI.Pages;

public partial class MainPage : ContentPage
{
    // Strona dostaje konfigurację przez konstruktor – tworzy ją kontener (MauiProgram)
    public MainPage(IConfiguration configuration)
    {
        InitializeComponent();
        VersionLabel.Text = $"wersja {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        ApiLabel.Text = $"API: {configuration["Api:BaseUrl"] ?? "(brak w appsettings.json)"}";
    }
}
