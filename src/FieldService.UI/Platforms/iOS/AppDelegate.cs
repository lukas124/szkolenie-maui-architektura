using Foundation;

namespace FieldService.UI;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // Deep link: fieldservice://visits/{id} (schemat w Info.plist, CFBundleURLTypes) → App.OnAppLinkRequestReceived
    public override bool OpenUrl(UIKit.UIApplication application, NSUrl url, NSDictionary options)
    {
        if (Uri.TryCreate(url.AbsoluteString, UriKind.Absolute, out var uri))
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(uri);
        return base.OpenUrl(application, url, options);
    }
}
