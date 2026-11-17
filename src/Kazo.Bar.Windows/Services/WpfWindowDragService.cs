using System.Windows.Input;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Layout;
using static Kazo.Bar.Windows.Services.NativeMethods;

namespace Kazo.Bar.Windows.Services;

/// <summary>
/// Moves the toolbar window from the web drag handle (native DragMove, no mouse polling),
/// then keeps the pill — and therefore the handle at its right end — inside the work area.
/// </summary>
public sealed class WpfWindowDragService(ToolbarHost host, IDockingService docking) : IWindowDragService
{
    /// <summary>Used until the page has reported the pill: the top strip of the surface.</summary>
    private static readonly (double Left, double Top, double Right, double Bottom) DefaultPill =
        (0, 0, ToolbarWindow.SurfaceWidth, 48);

    private (double Left, double Top, double Right, double Bottom) _lastPill = DefaultPill;

    public bool CanDrag => host.Window != null && !docking.IsDocked;

    /// <summary>True while the native move loop runs (the user holds the handle).</summary>
    public bool IsDragging { get; private set; }

    public Task DragAsync()
    {
        var window = host.Window;
        if (window == null || docking.IsDocked) return Task.CompletedTask;
        return window.Dispatcher.InvokeAsync(() =>
        {
            // The pointerdown travelled through the web page; only start if the button is still down.
            if (Mouse.LeftButton != MouseButtonState.Pressed) return;
            IsDragging = true;
            try { window.DragMove(); }
            catch (InvalidOperationException ex) { Program.Log($"Drag not started: {ex.Message}"); }
            finally { IsDragging = false; }
        }).Task;
    }

    public void KeepInWorkArea(double left, double top, double right, double bottom)
    {
        if (right > left && bottom > top) _lastPill = (left, top, right, bottom);
        var window = host.Window;
        if (window == null) return;
        window.Dispatcher.Invoke(ClampLastPill);
    }

    /// <summary>
    /// Re-applies the last known pill rectangle on the window's current monitor, e.g. after
    /// a DPI change moved the window onto a screen with a different work area.
    /// Skipped while dragging: the page clamps again when the button is released.
    /// </summary>
    internal void ClampLastPill()
    {
        var hwnd = host.Hwnd;
        if (hwnd == nint.Zero || docking.IsDocked || IsDragging) return;
        if (!GetWindowRect(hwnd, out var rect)) return;

        var scale = GetDpi(hwnd) / 96.0;
        var window = new PixelRect(rect.left, rect.top, rect.right, rect.bottom);
        var area = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea; // physical px
        var work = new PixelRect(area.Left, area.Top, area.Right, area.Bottom);

        var pill = ScreenFit.ToScreen(window, _lastPill.Left, _lastPill.Top, _lastPill.Right, _lastPill.Bottom, scale);
        var (dx, dy) = ScreenFit.OffsetIntoWorkArea(pill, work);
        if (dx == 0 && dy == 0) return;

        Program.Log($"Keeping toolbar on screen: offset ({dx}, {dy}) px.");
        SetWindowPos(hwnd, nint.Zero, rect.left + dx, rect.top + dy, 0, 0,
            SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
    }
}
