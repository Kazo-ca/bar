using System.Windows;
using System.Windows.Media;
using Kazo.Bar.Core.Dialogs;
using Kazo.Bar.UI.Dialogs;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Kazo.Bar.Windows;

/// <summary>
/// A dialog in its own normal (opaque, resizable, taskbar) window. Its BlazorWebView renders
/// <see cref="DialogWindowRoot"/>, which shows the requested component with the DialogContext.
/// Opened by <see cref="Services.WpfDialogService"/>.
/// </summary>
public sealed class DialogWindow : Window
{
    private const double MinimumWidth = 360;
    private const double MinimumHeight = 240;
    private static readonly Color Background_ = Color.FromRgb(0x1E, 0x1E, 0x1E);

    private readonly BlazorWebView _webView;

    public DialogWindow(IServiceProvider services, DialogContext context, Type componentType,
        IReadOnlyDictionary<string, object?>? parameters, DialogOptions options)
    {
        Context = context;
        Title = string.IsNullOrWhiteSpace(options.Title) ? "Kazo Bar" : options.Title;
        var work = SystemParameters.WorkArea;
        Width = Math.Clamp(options.Width, MinimumWidth, Math.Max(MinimumWidth, work.Width));
        Height = Math.Clamp(options.Height, MinimumHeight, Math.Max(MinimumHeight, work.Height));
        MinWidth = MinimumWidth;
        MinHeight = MinimumHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Background = new SolidColorBrush(Background_);

        _webView = new BlazorWebView
        {
            HostPage = @"wwwroot\dialog.html",
            Services = services,
            Background = Background,
        };
        _webView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(DialogWindowRoot),
            Parameters = DialogWindowRoot.Parameters(context, componentType, parameters),
        });
        _webView.BlazorWebViewInitialized += (_, e) =>
        {
            // Same color as the page: no white flash while it loads.
            e.WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, Background_.R, Background_.G, Background_.B);
            e.WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            e.WebView.CoreWebView2.ProcessFailed += (_, failure) =>
                Program.Log($"Dialog {context.Key}: WebView2 process failed: {failure.ProcessFailedKind} ({failure.Reason})");
        };
        Content = _webView;

        Closed += async (_, _) =>
        {
            IsClosed = true;
            Program.Log($"Dialog closed: {context.Key}.");
            await _webView.DisposeAsync();
        };
    }

    public DialogContext Context { get; }

    public bool IsClosed { get; private set; }

    /// <summary>Shows the window again (restored, in front) when its dialog is opened a second time.</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        if (!IsVisible) Show();
        Activate();
    }
}
