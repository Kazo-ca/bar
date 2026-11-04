using System.Windows.Input;
using Kazo.Bar.Core.Interfaces;
using static Kazo.Bar.Windows.Services.NativeMethods;

namespace Kazo.Bar.Windows.Services;

/// <summary>
/// Moves the toolbar window from the web drag handle (native DragMove, no mouse polling),
/// then keeps the pill — and therefore the handle at its right end — inside the work area.
/// </summary>
public sealed class WpfWindowDragService(ToolbarHost host, IDockingService docking) : IWindowDragService
{
    public bool CanDrag => host.Window != null && !docking.IsDocked;

    public Task DragAsync()
    {
        var window = host.Window;
        if (window == null || docking.IsDocked) return Task.CompletedTask;
        return window.Dispatcher.InvokeAsync(() =>
        {
            // The pointerdown travelled through the web page; only start if the button is still down.
            if (Mouse.LeftButton != MouseButtonState.Pressed) return;
            try { window.DragMove(); }
            catch (InvalidOperationException ex) { Program.Log($"Drag not started: {ex.Message}"); }
        }).Task;
    }

    public void KeepInWorkArea(double left, double top, double right, double bottom)
    {
        var window = host.Window;
        var hwnd = host.Hwnd;
        if (window == null || hwnd == nint.Zero || docking.IsDocked) return;
        window.Dispatcher.Invoke(() =>
        {
            if (!GetWindowRect(hwnd, out var rect)) return;
            var scale = GetDpi(hwnd) / 96.0;
            var work = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea; // physical px

            // Pill rectangle in screen physical pixels.
            int pl = rect.left + (int)Math.Floor(left * scale);
            int pt = rect.top + (int)Math.Floor(top * scale);
            int pr = rect.left + (int)Math.Ceiling(right * scale);
            int pb = rect.top + (int)Math.Ceiling(bottom * scale);

            int dx = pl < work.Left ? work.Left - pl : pr > work.Right ? work.Right - pr : 0;
            int dy = pt < work.Top ? work.Top - pt : pb > work.Bottom ? work.Bottom - pb : 0;
            if (dx == 0 && dy == 0) return;

            Program.Log($"Keeping toolbar on screen: offset ({dx}, {dy}) px.");
            SetWindowPos(hwnd, nint.Zero, rect.left + dx, rect.top + dy, 0, 0,
                SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
        });
    }
}
