using System.Runtime.InteropServices;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Kazo.Bar.Maui.Platforms.Windows.Services;

/// <summary>
/// Windows AppBar service for docking the toolbar to screen edges across multiple monitors.
/// Uses SHAppBarMessage and SetWindowPos for precise window placement.
/// </summary>
public class WindowsAppBarService : IDockingService
{
    private nint _hWnd;
    private bool _isDocked;
    private readonly int _uCallBack;
    private readonly IWindowService _windowService;

    public bool IsDocked => _isDocked;

    public WindowsAppBarService(IWindowService windowService)
    {
        _windowService = windowService;
        _uCallBack = RegisterWindowMessage("KazoBarAppBarCallbackMsg");
    }

    public void DockWindow(DockEdge edge, int monitorIndex, int heightPixels)
    {
        EnsureHwnd();

        var monitors = _windowService.GetMonitors();
        if (monitorIndex < 0 || monitorIndex >= monitors.Count)
            monitorIndex = 0;

        var monitor = monitors[monitorIndex];

        // Prepare APPBARDATA
        var abd = new APPBARDATA();
        abd.cbSize = Marshal.SizeOf<APPBARDATA>();
        abd.hWnd = _hWnd;
        abd.uCallbackMessage = _uCallBack;

        // Register the AppBar if not already done
        if (!_isDocked)
        {
            SHAppBarMessage(ABM_NEW, ref abd);
            _isDocked = true;
        }

        // Set edge (1=Top, 3=Bottom matches Win32 ABE_ constants)
        abd.uEdge = (int)edge;

        // Calculate coordinates based on the target monitor's bounds
        var bounds = monitor.Bounds;

        if (edge == DockEdge.Top)
        {
            abd.rc.left = bounds.Left;
            abd.rc.right = bounds.Right;
            abd.rc.top = bounds.Top;
            abd.rc.bottom = bounds.Top + heightPixels;
        }
        else // Bottom
        {
            abd.rc.left = bounds.Left;
            abd.rc.right = bounds.Right;
            abd.rc.bottom = bounds.Bottom;
            abd.rc.top = bounds.Bottom - heightPixels;
        }

        // Query the system for available space
        SHAppBarMessage(ABM_QUERYPOS, ref abd);

        // Apply the final position
        SHAppBarMessage(ABM_SETPOS, ref abd);

        // Move the actual window to the reserved area
        SetWindowPos(
            _hWnd,
            HWND_TOPMOST,
            abd.rc.left,
            abd.rc.top,
            abd.rc.right - abd.rc.left,
            abd.rc.bottom - abd.rc.top,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public void Undock()
    {
        if (_isDocked && _hWnd != nint.Zero)
        {
            var abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf<APPBARDATA>();
            abd.hWnd = _hWnd;
            SHAppBarMessage(ABM_REMOVE, ref abd);
            _isDocked = false;
        }
    }

    public void PositionOnMonitor(DockEdge edge, int monitorIndex, int heightPixels)
    {
        EnsureHwnd();

        // Make sure we're not registered as AppBar
        Undock();

        var monitors = _windowService.GetMonitors();
        if (monitorIndex < 0 || monitorIndex >= monitors.Count)
            monitorIndex = 0;

        var monitor = monitors[monitorIndex];
        var bounds = monitor.Bounds;

        int x = bounds.Left;
        int y;
        int width = bounds.Width;

        if (edge == DockEdge.Top)
        {
            y = bounds.Top;
        }
        else
        {
            y = bounds.Bottom - heightPixels;
        }

        SetWindowPos(
            _hWnd,
            HWND_TOPMOST,
            x, y, width, heightPixels,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
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
                // In floating mode, restore normal window behavior
                if (_hWnd != nint.Zero)
                {
                    SetWindowPos(
                        _hWnd,
                        HWND_TOPMOST,
                        0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
                break;
        }
    }

    private void EnsureHwnd()
    {
        if (_hWnd != nint.Zero) return;

        var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            _hWnd = WindowNative.GetWindowHandle(nativeWindow);
        }

        if (_hWnd == nint.Zero)
            throw new InvalidOperationException("Cannot obtain the native window handle.");
    }

    /// <summary>
    /// Gets the current HWND (for other services to use, e.g., WindowsWindowService).
    /// </summary>
    public nint GetWindowHandle()
    {
        EnsureHwnd();
        return _hWnd;
    }

    #region P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public nint hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public nint lParam;
    }

    // ABM message constants
    private const int ABM_NEW = 0;
    private const int ABM_REMOVE = 1;
    private const int ABM_QUERYPOS = 2;
    private const int ABM_SETPOS = 3;

    // SetWindowPos constants
    private static readonly nint HWND_TOPMOST = new(-1);
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    [DllImport("shell32.dll")]
    private static extern nint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    #endregion
}
