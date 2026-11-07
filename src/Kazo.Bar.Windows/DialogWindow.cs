using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kazo.Bar.Core.Dialogs;
using Kazo.Bar.UI.Dialogs;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Web.WebView2.Core;

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

    /// <summary>CSS px scrolled per wheel notch (about what Edge scrolls for one notch).</summary>
    private const double WheelPixelsPerNotch = 100;

    private readonly BlazorWebView _webView;
    private CoreWebView2? _core;

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
            _core = e.WebView.CoreWebView2;
            e.WebView.CoreWebView2.ProcessFailed += (_, failure) =>
                Program.Log($"Dialog {context.Key}: WebView2 process failed: {failure.ProcessFailedKind} ({failure.Reason})");
        };
        Content = _webView;
        _webView.PreviewMouseWheel += OnPreviewMouseWheel;

        Closed += async (_, _) =>
        {
            IsClosed = true;
            _core = null;
            Program.Log($"Dialog closed: {context.Key}.");
            try
            {
                await _webView.DisposeAsync();
            }
            catch (Exception ex)
            {
                // async void handler: never let a teardown error reach the dispatcher.
                Program.Log($"Dialog {context.Key}: WebView dispose failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Mouse wheel. WebView2CompositionControl forwards the wheel to the browser, but the page of
    /// a dialog window did not scroll with it. The window takes the wheel instead (Handled, so the
    /// control does not forward it as well) and asks the page to scroll what is under the pointer
    /// (wwwroot/js/wpf-dialog.js). Shift + wheel scrolls horizontally, as in a browser.
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_core is not { } core) return;
        e.Handled = true;

        var position = e.GetPosition(_webView);
        var pixels = -e.Delta / (double)Mouse.MouseWheelDeltaForOneLine * WheelPixelsPerNotch;
        var horizontal = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var script = string.Format(CultureInfo.InvariantCulture,
            "window.kazoDialogWheel && window.kazoDialogWheel({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##});",
            position.X, position.Y, horizontal ? pixels : 0, horizontal ? 0 : pixels);
        _ = ScrollAsync(core, script);
    }

    private async Task ScrollAsync(CoreWebView2 core, string script)
    {
        try
        {
            await core.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            // Window closing or page reloading: one lost wheel notch is harmless.
            Program.Log($"Dialog {Context.Key}: wheel scroll failed: {ex.Message}");
        }
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
