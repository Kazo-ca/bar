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

    /// <summary>Called once the HWND exists: follows moves and DPI changes.</summary>
    internal void Attach(Window window)
    {
        window.LocationChanged += (_, _) => Refresh();
        window.DpiChanged += (_, _) => Refresh();
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
            var scale = GetDpi(hwnd) / 96.0;
            int width = (int)Math.Round(target.Width * scale);
            int height = (int)Math.Round(target.Height * scale);
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
