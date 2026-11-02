using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DevToolbar.Feasibility;

internal static class NativeWindow
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    {
        public uint Size; public Rect Monitor, Work; public uint Flags;
    }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    public static object TargetAt(Point point)
    {
        var hit = WindowFromPoint(new NativePoint { X = (int)Math.Round(point.X), Y = (int)Math.Round(point.Y) });
        var root = GetAncestor(hit, 2);
        GetWindowThreadProcessId(hit, out var hitPid);
        GetWindowThreadProcessId(root, out var rootPid);
        return new { hwnd = hit.ToString("X"), rootHwnd = root.ToString("X"), hitPid, rootPid,
            rootIsThisProcess = rootPid == Environment.ProcessId };
    }

    public static bool ForegroundBelongsToThisProcess()
    {
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out var pid);
        return foreground == 0 || pid == Environment.ProcessId;
    }

    public static object Describe(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        GetWindowRect(handle, out var r);
        return new { hwnd = handle.ToString("X"), rect = new { r.Left, r.Top, r.Right, r.Bottom },
            dpi = GetDpiForWindow(handle), foreground = GetForegroundWindow().ToString("X"),
            extendedStyle = GetWindowLongPtr(handle, -20).ToString("X"),
            window.IsActive, window.IsVisible };
    }

    // Physical coordinates: no CSS input region or per-pixel hit-test is installed.
    public static void PlacePopup(Window bar, Window popup)
    {
        var anchor = bar.PointToScreen(new Point(300, 78));
        var hwnd = new WindowInteropHelper(bar).Handle;
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        var scale = GetDpiForWindow(hwnd) / 96.0;
        var width = (int)Math.Round(320 * scale);
        var height = (int)Math.Round(260 * scale);
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var x = Math.Clamp((int)Math.Round(anchor.X), info.Work.Left, Math.Max(info.Work.Left, info.Work.Right - width));
        var y = Math.Clamp((int)Math.Round(anchor.Y), info.Work.Top, Math.Max(info.Work.Top, info.Work.Bottom - height));
        SetWindowPos(new WindowInteropHelper(popup).Handle, 0, x, y, width, height, 0x0014); // NOZORDER | NOACTIVATE
    }
}
