using Microsoft.AspNetCore.Components.WebView.Maui;

namespace Kazo.Bar.Maui;

/// <summary>
/// Main page hosting the Blazor WebView.
/// </summary>
public class MainPage : ContentPage
{
    public MainPage()
    {
        // Transparent page background so the desktop shows through
        BackgroundColor = Colors.Transparent;

        var blazorWebView = new BlazorWebView
        {
            HostPage = "wwwroot/index.html",
            BackgroundColor = Colors.Transparent,
            RootComponents =
            {
                new RootComponent
                {
                    Selector = "#app",
                    ComponentType = typeof(Kazo.Bar.UI.Pages.Home)
                },
                new RootComponent
                {
                    Selector = "#dialogs",
                    ComponentType = typeof(Kazo.Bar.UI.Dialogs.DialogOutlet)
                }
            }
        };


#if WINDOWS
        // WebView2 only: the Android target has no DefaultBackgroundColor.
        blazorWebView.BlazorWebViewInitialized += (sender, args) =>
        {
            // Match WebView2 background to the toolbar glass background
            // This ensures no flash of a different color during load
            args.WebView.DefaultBackgroundColor =
                global::Windows.UI.Color.FromArgb(255, 200, 207, 218);
        };
#endif


        Content = blazorWebView;
    }
}
