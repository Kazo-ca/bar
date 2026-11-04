using System.Runtime.InteropServices;

namespace Kazo.Bar.Windows.Services;

internal static partial class NativeMethods
{
    public static bool ForegroundBelongsToCurrentProcess()
    {
        var foreground = GetForegroundWindow();
        if (foreground == nint.Zero) return false;
        GetWindowThreadProcessId(foreground, out var pid);
        return pid == (uint)Environment.ProcessId;
    }

    public static uint GetDpi(nint hwnd)
    {
        var dpi = hwnd == nint.Zero ? 0u : GetDpiForWindow(hwnd);
        return dpi == 0 ? 96u : dpi;
    }

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint hIcon);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hWnd, out RECT rect);

    /// <summary>
    /// Keeps the window out of the taskbar and Alt+Tab: adds WS_EX_TOOLWINDOW and
    /// drops WS_EX_APPWINDOW. The tray icon stays the way to reach the app.
    /// </summary>
    public static void HideFromTaskbarAndAltTab(nint hwnd)
    {
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        var updated = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        if (updated != exStyle) SetWindowLongPtr(hwnd, GWL_EXSTYLE, updated);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint newLong);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int RegisterWindowMessage(string name);

    [DllImport("shell32.dll")]
    public static extern nint SHAppBarMessage(int message, ref APPBARDATA data);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize;
        public nint hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public nint lParam;
    }

    public const int GWL_EXSTYLE = -20;
    public const nint WS_EX_TOOLWINDOW = 0x00000080, WS_EX_APPWINDOW = 0x00040000;
    public const int ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
    public static readonly nint HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
}
