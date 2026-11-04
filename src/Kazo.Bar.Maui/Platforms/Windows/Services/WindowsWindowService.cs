using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

namespace Kazo.Bar.Maui.Platforms.Windows.Services;

/// <summary>
/// Windows native implementation of IWindowService.
/// Uses P/Invoke to enumerate windows, detect monitors, extract icons,
/// and activate (focus) windows.
/// </summary>
public class WindowsWindowService : IWindowService
{
    private nint _ownHwnd = nint.Zero;

    /// <summary>
    /// Sets the HWND of the Kazo Bar window so it can be excluded from enumeration.
    /// </summary>
    public void SetOwnWindowHandle(nint hwnd) => _ownHwnd = hwnd;

    #region IWindowService

    public Task<List<WindowInfo>> GetOpenWindowsOnMonitorAsync(int monitorIndex)
    {
        var monitors = GetMonitorHandles();
        if (monitorIndex < 0 || monitorIndex >= monitors.Count)
            return Task.FromResult(new List<WindowInfo>());

        var targetMonitor = monitors[monitorIndex];
        var windows = new List<WindowInfo>();

        EnumWindows((hWnd, lParam) =>
        {
            try
            {
                if (!IsVisibleAppWindow(hWnd))
                    return true; // continue

                // Skip our own toolbar window
                if (hWnd == _ownHwnd)
                    return true;

                // Check which monitor this window belongs to
                var windowMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                if (windowMonitor != targetMonitor)
                    return true;

                var title = GetWindowTitleSafe(hWnd);
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                var processName = GetProcessNameSafe(hWnd);
                var iconBase64 = GetWindowIconBase64(hWnd);
                var isActive = GetForegroundWindow() == hWnd;

                windows.Add(new WindowInfo
                {
                    Hwnd = hWnd,
                    Title = title,
                    ProcessName = processName,
                    IconBase64 = iconBase64,
                    IsActive = isActive
                });
            }
            catch
            {
                // Window may have closed during enumeration — skip
            }

            return true; // continue enumeration
        }, nint.Zero);

        return Task.FromResult(windows);
    }

    public void ActivateWindow(nint hWnd)
    {
        try
        {
            if (!IsWindow(hWnd))
                return;

            // Restore if minimized
            if (IsIconic(hWnd))
                ShowWindow(hWnd, SW_RESTORE);

            // Bring to foreground
            SetForegroundWindow(hWnd);
        }
        catch
        {
            // Window no longer exists — silently ignore
        }
    }

    public List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        int index = 0;

        EnumDisplayMonitors(nint.Zero, nint.Zero, (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
        {
            var mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf<MONITORINFOEX>();

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                monitors.Add(new MonitorInfo
                {
                    Index = index,
                    DeviceName = mi.szDevice,
                    IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    Bounds = new ScreenRect
                    {
                        Left = mi.rcMonitor.left,
                        Top = mi.rcMonitor.top,
                        Right = mi.rcMonitor.right,
                        Bottom = mi.rcMonitor.bottom
                    },
                    WorkArea = new ScreenRect
                    {
                        Left = mi.rcWork.left,
                        Top = mi.rcWork.top,
                        Right = mi.rcWork.right,
                        Bottom = mi.rcWork.bottom
                    }
                });
                index++;
            }
            return true;
        }, nint.Zero);

        // Ensure primary monitor is first
        monitors.Sort((a, b) =>
        {
            if (a.IsPrimary && !b.IsPrimary) return -1;
            if (!a.IsPrimary && b.IsPrimary) return 1;
            return a.Index.CompareTo(b.Index);
        });

        // Reassign indices after sorting
        for (int i = 0; i < monitors.Count; i++)
            monitors[i].Index = i;

        return monitors;
    }

    #endregion

    #region Private helpers

    /// <summary>
    /// Gets a list of monitor handles in the same order as GetMonitors().
    /// </summary>
    private List<nint> GetMonitorHandles()
    {
        var handles = new List<(nint Handle, bool IsPrimary)>();

        EnumDisplayMonitors(nint.Zero, nint.Zero, (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
        {
            var mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf<MONITORINFOEX>();
            bool isPrimary = false;
            if (GetMonitorInfo(hMonitor, ref mi))
                isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;

            handles.Add((hMonitor, isPrimary));
            return true;
        }, nint.Zero);

        // Sort the same way as GetMonitors: primary first
        handles.Sort((a, b) =>
        {
            if (a.IsPrimary && !b.IsPrimary) return -1;
            if (!a.IsPrimary && b.IsPrimary) return 1;
            return 0;
        });

        return handles.Select(h => h.Handle).ToList();
    }

    /// <summary>
    /// Determines whether a window is a visible, top-level application window.
    /// Excludes: invisible windows, tool windows, cloaked UWP windows, tooltips, etc.
    /// </summary>
    private static bool IsVisibleAppWindow(nint hWnd)
    {
        if (!IsWindowVisible(hWnd))
            return false;

        // Exclude windows with WS_EX_TOOLWINDOW (floating toolbars, palettes)
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            return false;

        // Must have an owner of IntPtr.Zero (top-level) or WS_EX_APPWINDOW
        var owner = GetWindow(hWnd, GW_OWNER);
        if (owner != nint.Zero && (exStyle & WS_EX_APPWINDOW) == 0)
            return false;

        // Exclude cloaked windows (UWP background apps, virtual desktops)
        if (IsWindowCloaked(hWnd))
            return false;

        // Exclude windows with no title
        int titleLength = GetWindowTextLength(hWnd);
        if (titleLength == 0)
            return false;

        return true;
    }

    private static bool IsWindowCloaked(nint hWnd)
    {
        int cloaked = 0;
        int hr = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, ref cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    private static string GetWindowTitleSafe(nint hWnd)
    {
        try
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;

            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetProcessNameSafe(nint hWnd)
    {
        try
        {
            GetWindowThreadProcessId(hWnd, out uint processId);
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetWindowIconBase64(nint hWnd)
    {
        try
        {
            // Try WM_GETICON (large icon first, then small)
            var iconHandle = SendMessage(hWnd, WM_GETICON, ICON_BIG, nint.Zero);
            if (iconHandle == nint.Zero)
                iconHandle = SendMessage(hWnd, WM_GETICON, ICON_SMALL, nint.Zero);
            if (iconHandle == nint.Zero)
                iconHandle = SendMessage(hWnd, WM_GETICON, ICON_SMALL2, nint.Zero);

            // Try GetClassLongPtr as fallback
            if (iconHandle == nint.Zero)
                iconHandle = GetClassLongPtr(hWnd, GCLP_HICON);
            if (iconHandle == nint.Zero)
                iconHandle = GetClassLongPtr(hWnd, GCLP_HICONSM);

            if (iconHandle == nint.Zero)
                return string.Empty;

            return ConvertIconToBase64(iconHandle);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ConvertIconToBase64(nint hIcon)
    {
        try
        {
            using var icon = System.Drawing.Icon.FromHandle(hIcon);
            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion

    #region P/Invoke declarations

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);
    private delegate bool MonitorEnumDelegate(nint hMonitor, nint hdcMonitor, nint lprcMonitor, nint dwData);

    // --- user32.dll ---

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hWnd, uint uCmd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumDelegate lpfnEnum, nint dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern nint GetClassLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    // --- dwmapi.dll ---

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    // --- Constants ---

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const uint GW_OWNER = 4;
    private const int DWMWA_CLOAKED = 14;
    private const int SW_RESTORE = 9;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MONITORINFOF_PRIMARY = 1;

    // WM_GETICON constants
    private const uint WM_GETICON = 0x007F;
    private static readonly nint ICON_SMALL = 0;
    private static readonly nint ICON_BIG = 1;
    private static readonly nint ICON_SMALL2 = 2;

    // GetClassLongPtr constants
    private const int GCLP_HICON = -14;
    private const int GCLP_HICONSM = -34;

    // --- Structs ---

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    #endregion
}
