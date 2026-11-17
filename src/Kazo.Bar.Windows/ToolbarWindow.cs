using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Maui.Platforms.Windows.Services;
using Kazo.Bar.UI.Services;
using Kazo.Bar.Windows.Services;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Bar.Windows;

/// <summary>
/// The single toolbar window.
///
/// WPF layered window (<see cref="Window.AllowsTransparency"/>) hosting the
/// official WPF <see cref="BlazorWebView"/> (WebView2CompositionControl inside).
/// Hit-testing follows per-pixel alpha: transparent areas around the CSS shapes
/// pass clicks and wheel to the application below. This is the configuration
/// validated manually in spikes/window-feasibility (no-shadow variant).
///
/// Deliberately absent: SetWindowRgn, computed input masks, mouse polling,
/// global hooks, invisible overlays that would eat the first outside click.
/// </summary>
public sealed class ToolbarWindow : Window
{
    /// <summary>Logical size of the web surface: toolbar strip + room for menus.</summary>
    public const double SurfaceWidth = 960;
    public const double SurfaceHeight = 500;

    private readonly IServiceProvider _services;
    private readonly ToolbarHost _host;
    private readonly PopupDismissService _dismiss;
    private readonly BlazorWebView _webView;

    public ToolbarWindow(IServiceProvider services)
    {
        _services = services;
        _host = services.GetRequiredService<ToolbarHost>();
        _dismiss = services.GetRequiredService<PopupDismissService>();

        Title = "Kazo Bar";
        Width = SurfaceWidth;
        Height = SurfaceHeight;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        // No taskbar button: the tray icon is the way to show / hide / quit.
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _webView = new BlazorWebView
        {
            HostPage = @"wwwroot\index.html",
            Services = services,
            Background = Brushes.Transparent,
        };
        _webView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Routes),
        });
        _webView.BlazorWebViewInitialized += (_, e) =>
        {
            // Alpha 0: the page background must not paint (and therefore not capture input).
            e.WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            _host.AttachWebView(e.WebView.CoreWebView2);
            Program.Log($"WebView2 ready: {e.WebView.CoreWebView2.Environment.BrowserVersionString} ({e.WebView.GetType().Name})");
        };
        Content = _webView;

        // Diagnostics: one line per press, to tell whether a click reached this window at all.
        PreviewMouseDown += (_, e) => Program.Log(
            $"Mouse {e.ChangedButton} down at {e.GetPosition(this)} on {e.OriginalSource?.GetType().Name}");

        SourceInitialized += OnSourceInitialized;
        Deactivated += OnDeactivated;
        LocationChanged += (_, _) =>
        {
            // Our own resize (component asked for more room) is not a user move.
            if (!_host.IsAdjustingSurface) _dismiss.RequestDismiss(PopupDismissReason.HostMoved);
        };
        Closed += async (_, _) =>
        {
            _services.GetService<IDockingService>()?.Undock();
            await _webView.DisposeAsync();
        };
    }

    private async void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        Program.Log($"Window created, HWND=0x{hwnd:X}.");
        // Tool window: also out of Alt+Tab (a borderless strip there would be confusing).
        NativeMethods.HideFromTaskbarAndAltTab(hwnd);
        _host.Attach(this, hwnd);
        _services.GetRequiredService<WindowsWindowService>().SetOwnWindowHandle(hwnd);

        // Default placement: centered at the top of the primary work area.
        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top;

        // Components may now ask for more room (e.g. the Settings page).
        _services.GetRequiredService<WpfHostSurfaceService>().Attach(this);

        try
        {
            var globalSettings = _services.GetRequiredService<IGlobalSettingsService>();
            await globalSettings.LoadAsync();
            var docking = _services.GetRequiredService<IDockingService>();
            docking.ApplySettings(globalSettings.Current);
            globalSettings.OnSettingsChanged += settings => Dispatcher.Invoke(() => docking.ApplySettings(settings));
        }
        catch (Exception ex)
        {
            Program.Log($"Docking settings not applied: {ex}");
        }
    }

    /// <summary>
    /// The window reached a monitor with another scale (usually mid-drag). Once WPF has
    /// applied the new DPI: fix the physical size, let the web view re-measure at the new
    /// scale, and bring the pill back inside the new monitor's work area.
    /// </summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Program.Log($"DPI changed {oldDpi.PixelsPerInchX:0} -> {newDpi.PixelsPerInchX:0}.");
        _dismiss.RequestDismiss(PopupDismissReason.HostMoved);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ReconcileAfterDpiChange);
    }

    private void ReconcileAfterDpiChange()
    {
        _services.GetRequiredService<WpfHostSurfaceService>().ReapplyAfterDpiChange();

        // The composition web view only re-reads its scale on a layout change; a DPI change
        // alone keeps the DIP size identical. A one-pixel nudge forces it to re-measure.
        _webView.Margin = new Thickness(0, 0, 1, 0);
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            _webView.Margin = new Thickness(0);
            (_services.GetService<IWindowDragService>() as WpfWindowDragService)?.ClampLastPill();
            _host.LogPageScale();
        });
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Let activation settle (spike behaviour): if focus went to another
        // process, close popups without taking focus back.
        Dispatcher.BeginInvoke(() =>
        {
            if (!NativeMethods.ForegroundBelongsToCurrentProcess())
                _dismiss.RequestDismiss(PopupDismissReason.HostDeactivated);
        });
    }
}
