using System.Runtime.InteropServices;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using static Kazo.Bar.Windows.Services.NativeMethods;

namespace Kazo.Bar.Windows.Services;

/// <summary>
/// AppBar docking for the WPF host.
///
/// Difference with the MAUI WindowsAppBarService: the screen strip reserved
/// through SHAppBarMessage (toolbar height) is distinct from the window size.
/// The window keeps its full web surface (toolbar + room for menus) so that
/// dropdowns are not cut; the transparent part below/above the toolbar stays
/// click-through, so it does not block the windows under it.
/// </summary>
public sealed class WpfAppBarService : IDockingService
{
    private readonly ToolbarHost _host;
    private readonly IWindowService _windowService;
    private readonly int _callbackMessage;
    private bool _isDocked;

    public WpfAppBarService(ToolbarHost host, IWindowService windowService)
    {
        _host = host;
        _windowService = windowService;
        _callbackMessage = RegisterWindowMessage("KazoBarAppBarCallbackMsg");
    }

    public bool IsDocked => _isDocked;

    public void DockWindow(DockEdge edge, int monitorIndex, int heightPixels)
    {
        var hwnd = RequireHwnd();
        var monitor = GetMonitor(monitorIndex);
        var scale = GetDpi(hwnd) / 96.0;
        var reserved = (int)Math.Round(heightPixels * scale);

        var abd = NewData(hwnd);
        if (!_isDocked)
        {
            SHAppBarMessage(ABM_NEW, ref abd);
            _isDocked = true;
        }

        abd.uEdge = (int)edge;
        abd.rc.left = monitor.Bounds.Left;
        abd.rc.right = monitor.Bounds.Right;
        if (edge == DockEdge.Top)
        {
            abd.rc.top = monitor.Bounds.Top;
            abd.rc.bottom = monitor.Bounds.Top + reserved;
        }
        else
        {
            abd.rc.bottom = monitor.Bounds.Bottom;
            abd.rc.top = monitor.Bounds.Bottom - reserved;
        }
        SHAppBarMessage(ABM_QUERYPOS, ref abd);
        SHAppBarMessage(ABM_SETPOS, ref abd);

        // Place the (taller) window so the toolbar strip covers the reserved rect.
        var (width, height) = SurfacePixels(scale);
        int x = abd.rc.left + (abd.rc.right - abd.rc.left - width) / 2;
        int y = edge == DockEdge.Top ? abd.rc.top : abd.rc.bottom - height;
        _host.SetBodyClass("dock-bottom", edge == DockEdge.Bottom);
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public void Undock()
    {
        if (!_isDocked || _host.Hwnd == nint.Zero) return;
        var abd = NewData(_host.Hwnd);
        SHAppBarMessage(ABM_REMOVE, ref abd);
        _isDocked = false;
    }

    public void PositionOnMonitor(DockEdge edge, int monitorIndex, int heightPixels)
    {
        var hwnd = RequireHwnd();
        Undock();
        var monitor = GetMonitor(monitorIndex);
        var (width, height) = SurfacePixels(GetDpi(hwnd) / 96.0);
        int x = monitor.Bounds.Left + (monitor.Bounds.Width - width) / 2;
        int y = edge == DockEdge.Top ? monitor.Bounds.Top : monitor.Bounds.Bottom - height;
        _host.SetBodyClass("dock-bottom", edge == DockEdge.Bottom);
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public void ApplySettings(GlobalSettings settings)
    {
        switch (settings.ToolbarBehavior)
        {
            case ToolbarBehavior.DockedTop:
                DockWindow(DockEdge.Top, settings.TargetMonitorIndex, settings.ToolbarHeight);
                break;
            case ToolbarBehavior.DockedBottom:
                DockWindow(DockEdge.Bottom, settings.TargetMonitorIndex, settings.ToolbarHeight);
                break;
            case ToolbarBehavior.Overlay:
                PositionOnMonitor(DockEdge.Top, settings.TargetMonitorIndex, settings.ToolbarHeight);
                break;
            case ToolbarBehavior.Floating:
            default:
                Undock();
                _host.SetBodyClass("dock-bottom", false);
                if (_host.Hwnd != nint.Zero)
                    SetWindowPos(_host.Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                break;
        }
    }

    // Current surface (base size, or larger while a component asked for more room).
    private (int Width, int Height) SurfacePixels(double scale) =>
        ((int)Math.Round(_host.Surface.Width * scale), (int)Math.Round(_host.Surface.Height * scale));

    private MonitorInfo GetMonitor(int index)
    {
        var monitors = _windowService.GetMonitors();
        if (monitors.Count == 0) throw new InvalidOperationException("No monitor found.");
        return index >= 0 && index < monitors.Count ? monitors[index] : monitors[0];
    }

    private nint RequireHwnd() =>
        _host.Hwnd != nint.Zero ? _host.Hwnd : throw new InvalidOperationException("Toolbar window handle not available yet.");

    private APPBARDATA NewData(nint hwnd) => new()
    {
        cbSize = Marshal.SizeOf<APPBARDATA>(),
        hWnd = hwnd,
        uCallbackMessage = _callbackMessage,
    };
}
