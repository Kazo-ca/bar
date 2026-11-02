using System.Runtime.InteropServices;

namespace DevToolbar.Feasibility;

/// <summary>One-shot native environment capture; no hooks, timers or cursor polling.</summary>
public static class EnvironmentSnapshot
{
    public static object Capture()
    {
        var monitors = new List<object>();
        var errors = new List<string>();
        MonitorEnum callback = (nint monitor, nint hdc, ref Rect rect, nint data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = string.Empty };
            if (!GetMonitorInfo(monitor, ref info))
            {
                errors.Add($"GetMonitorInfo: {Marshal.GetLastWin32Error()}");
                return true;
            }
            uint? dpiX = null, dpiY = null;
            int? dpiHresult = null;
            try
            {
                var result = GetDpiForMonitor(monitor, 0, out var x, out var y);
                dpiHresult = result;
                if (result == 0) { dpiX = x; dpiY = y; }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                errors.Add($"GetDpiForMonitor: {ex.GetType().Name}");
            }
            monitors.Add(new
            {
                device = info.Device,
                primary = (info.Flags & 1) != 0,
                bounds = ToObject(info.Monitor),
                workArea = ToObject(info.Work),
                effectiveDpiX = dpiX,
                effectiveDpiY = dpiY,
                dpiHresult,
            });
            return true;
        };
        if (!EnumDisplayMonitors(0, 0, callback, 0))
            errors.Add($"EnumDisplayMonitors: {Marshal.GetLastWin32Error()}");

        uint? routing = ReadSetting(0x201C, "SPI_GETMOUSEWHEELROUTING", errors);
        uint? wheelLines = ReadSetting(0x0068, "SPI_GETWHEELSCROLLLINES", errors);
        uint? wheelCharacters = ReadSetting(0x006C, "SPI_GETWHEELSCROLLCHARS", errors);
        return new
        {
            capturedUtc = DateTimeOffset.UtcNow,
            os = RuntimeInformation.OSDescription,
            osVersion = Environment.OSVersion.Version.ToString(),
            framework = RuntimeInformation.FrameworkDescription,
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            monitors,
            doubleClickMilliseconds = GetDoubleClickTime(),
            doubleClickRectanglePixels = new { width = GetSystemMetrics(36), height = GetSystemMetrics(37) },
            mouseWheelRouting = routing,
            mouseWheelRoutingLegend = "0=focus, 1=hybrid, 2=mouse position",
            wheelScrollLines = wheelLines,
            wheelScrollCharacters = wheelCharacters,
            scrollPageSentinel = uint.MaxValue,
            remoteSession = GetSystemMetrics(0x1000) != 0,
            swappedMouseButtons = GetSystemMetrics(23) != 0,
            virtualScreenPixels = new { left = GetSystemMetrics(76), top = GetSystemMetrics(77),
                width = GetSystemMetrics(78), height = GetSystemMetrics(79) },
            dpiNote = "GetDpiForMonitor(MDT_EFFECTIVE_DPI); interpret with the process DPI awareness. Window logs report their actual WPF DPI.",
            errors,
        };
    }

    private static uint? ReadSetting(uint action, string name, List<string> errors)
    {
        if (SystemParametersInfo(action, 0, out var value, 0)) return value;
        errors.Add($"{name}: {Marshal.GetLastWin32Error()}");
        return null;
    }

    private static object ToObject(Rect rect) => new
    {
        left = rect.Left, top = rect.Top, right = rect.Right, bottom = rect.Bottom,
        width = rect.Right - rect.Left, height = rect.Bottom - rect.Top,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }

    private delegate bool MonitorEnum(nint monitor, nint hdc, ref Rect rect, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnum callback, nint data);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, out uint value, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
