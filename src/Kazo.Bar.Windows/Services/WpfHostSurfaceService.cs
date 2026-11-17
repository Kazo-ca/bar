using System.Windows;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Layout;
using Kazo.Bar.Core.Models;
using static Kazo.Bar.Windows.Services.NativeMethods;

namespace Kazo.Bar.Windows.Services;

/// <summary>
/// Grows or shrinks the toolbar window to what the page components ask for
/// (see <see cref="IHostSurfaceService"/>), within the work area of its monitor.
///
/// The window keeps the edge it is anchored to: the top normally, the bottom when
/// docked at the bottom of the screen (menus open upward there). It stays centered
/// horizontally. Extra height is transparent outside the CSS shapes, so it stays
/// click-through like the rest of the surface.
/// </summary>
public sealed class WpfHostSurfaceService(ToolbarHost host) : IHostSurfaceService
{
    private readonly SurfaceSizeNegotiator _negotiator =
        new(new SurfaceSize(ToolbarWindow.SurfaceWidth, ToolbarWindow.SurfaceHeight));

    public SurfaceSize Current => host.Surface;
    public SurfaceSize Available => _negotiator.Available;
    public event Action? Changed;

    /// <summary>Called once the HWND exists: follows moves (DPI changes go through <see cref="ReapplyAfterDpiChange"/>).</summary>
    internal void Attach(Window window)
    {
        window.LocationChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>
    /// After the window moved to a monitor with another scale: makes its physical size match
    /// the logical surface at the new DPI (WPF does not always resize a layered window to the
    /// suggested rectangle, which left the page clipped and the drag handle out of reach),
    /// then recomputes the space available on the new monitor.
    /// </summary>
    internal void ReapplyAfterDpiChange()
    {
        var hwnd = host.Hwnd;
        if (hwnd == nint.Zero || !GetWindowRect(hwnd, out var rect)) return;

        var scale = GetDpi(hwnd) / 96.0;
        var window = new PixelRect(rect.left, rect.top, rect.right, rect.bottom);
        if (ScreenFit.NeedsResize(window, host.Surface, scale))
        {
            var (width, height) = ScreenFit.ToPixels(host.Surface, scale);
            var target = ScreenFit.Resize(window, width, height, host.AnchorBottom);
            Program.Log($"DPI {scale * 96:0}: window {window.Width}x{window.Height} px resized to {width}x{height} px.");
            host.IsAdjustingSurface = true;
            try
            {
                SetWindowPos(hwnd, nint.Zero, target.Left, target.Top, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
            }
            finally
            {
                host.IsAdjustingSurface = false;
            }
        }
        Refresh();
    }

    public void RequestSize(string componentId, SurfaceSize desired) => OnUi(() =>
    {
        if (_negotiator.Request(componentId, desired)) Apply();
    });

    public void ReleaseSize(string componentId) => OnUi(() =>
    {
        if (_negotiator.Release(componentId)) Apply();
    });

    /// <summary>Recomputes the available space (window moved, docked, other monitor).</summary>
    private void Refresh()
    {
        if (host.IsAdjustingSurface || host.Hwnd == nint.Zero) return;
        if (!TryComputeAvailable(out var available) || available == _negotiator.Available) return;
        _negotiator.SetAvailable(available);
        Apply(force: true);
    }

    private void Apply(bool force = false)
    {
        var hwnd = host.Hwnd;
        if (hwnd == nint.Zero || !GetWindowRect(hwnd, out var rect)) return;

        var target = _negotiator.Target;
        if (target != host.Surface)
        {
            var (width, height) = ScreenFit.ToPixels(target, GetDpi(hwnd) / 96.0);
            int x = rect.left + (rect.right - rect.left - width) / 2;
            int y = host.AnchorBottom ? rect.bottom - height : rect.top;

            host.Surface = target;
            host.IsAdjustingSurface = true;
            try
            {
                SetWindowPos(hwnd, nint.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
            }
            finally
            {
                host.IsAdjustingSurface = false;
            }
            Program.Log($"Surface {target.Width}x{target.Height} (available {_negotiator.Available.Width}x{_negotiator.Available.Height}, {_negotiator.Requests.Count} request(s)).");
            force = true;
        }

        if (force) Changed?.Invoke();
    }

    private bool TryComputeAvailable(out SurfaceSize available)
    {
        available = default;
        var hwnd = host.Hwnd;
        if (!GetWindowRect(hwnd, out var rect)) return false;
        var scale = GetDpi(hwnd) / 96.0;
        var work = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea; // physical px

        // From the anchored edge to the far side of the work area. When docked, the
        // AppBar strip is outside the work area but the window starts on it, which is
        // what we want: the window may cover its own strip.
        int heightPx = host.AnchorBottom ? rect.bottom - work.Top : work.Bottom - rect.top;
        available = new SurfaceSize(
            Math.Floor(Math.Max(0, work.Width) / scale),
            Math.Floor(Math.Max(0, heightPx) / scale));
        return true;
    }

    private void OnUi(Action action)
    {
        var dispatcher = host.Window?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
