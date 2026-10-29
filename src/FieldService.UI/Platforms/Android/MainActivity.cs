using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;

namespace FieldService.UI;

// Deep link: fieldservice://visits/{id} otwiera aplikację (albo trafia do działającej – SingleTop → OnNewIntent)
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "fieldservice")]
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    // Zmiany konfiguracji, które aktywność obsługuje sama (obrót, motyw, rozmiar czcionki, język, klawiatura…).
    // Bez nich Android zamyka i tworzy aktywność od nowa, a razem z nią okno MAUI i cały stos stron.
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density |
                           ConfigChanges.FontScale | ConfigChanges.Locale | ConfigChanges.LayoutDirection |
                           ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        UpdateSystemBars(Resources?.Configuration); // ikony pasków systemowych
        SendAppLink(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        SendAppLink(intent);
    }

    // Adres z intencji → App.OnAppLinkRequestReceived (ta sama droga co na iOS)
    private static void SendAppLink(Intent? intent)
    {
        if (intent?.Data?.ToString() is { } link && Uri.TryCreate(link, UriKind.Absolute, out var uri))
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(uri);
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        UpdateSystemBars(newConfig); // zmiana trybu jasny/ciemny bez odtwarzania aktywności (ConfigChanges.UiMode)
    }

    // Pod paskami systemowymi (status, nawigacja) widać tło strony, więc ich ikony muszą pasować do motywu.
    // Przy zmianie motywu w trakcie działania aktywność nie jest tworzona od nowa (ConfigChanges.UiMode), więc przełączamy je tutaj.
    private void UpdateSystemBars(Configuration? configuration)
    {
        if (Window is null) return;
        var isNight = (configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
        SetBarIcons(Window, isNight);
    }

    // Ikony paska statusu i nawigacji: ciemne na jasnym tle, jasne na ciemnym
    private static void SetBarIcons(Android.Views.Window window, bool isNight)
    {
        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is null) return;
        controller.AppearanceLightStatusBars = !isNight;
        controller.AppearanceLightNavigationBars = !isNight;
    }
}
