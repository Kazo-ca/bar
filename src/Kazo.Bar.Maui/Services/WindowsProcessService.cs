using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

namespace Kazo.Bar.Maui.Services;

/// <summary>
/// Real Windows process service: starts programs and finds / activates their windows (US4.2).
/// Shared by the WPF and MAUI hosts.
/// </summary>
public partial class WindowsProcessService : IProcessService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    public async Task<int> StartProcessAsync(string path, string arguments = "")
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            UseShellExecute = true
        };

        var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {path}");

        return await Task.FromResult(process.Id);
    }

    public IReadOnlyList<WindowMatch> FindWindowsByTitle(string titleRegex)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(titleRegex))
            return [];

        Regex regex;
        try
        {
            regex = new Regex(titleRegex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return [];
        }

        var ownProcessId = (uint)Environment.ProcessId;
        var matches = new List<WindowMatch>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!IsAppWindow(hWnd))
                    return true;
                NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
                if (processId == ownProcessId)
                    return true;
                var title = GetTitle(hWnd);
                if (title.Length > 0 && regex.IsMatch(title))
                    matches.Add(new WindowMatch(hWnd, title, GetProcessName(processId)));
            }
            catch (RegexMatchTimeoutException)
            {
                // Pathological pattern for this title: skip the window.
            }
            return true;
        }, nint.Zero);
        return matches;
    }

    public bool FocusWindow(WindowMatch window)
    {
        if (!OperatingSystem.IsWindows() || !NativeMethods.IsWindow(window.Handle))
            return false;
        if (NativeMethods.IsIconic(window.Handle))
            NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);
        return NativeMethods.SetForegroundWindow(window.Handle);
    }

    /// <summary>Visible, titled, top-level window that is not a tool window or a cloaked (background) window.</summary>
    private static bool IsAppWindow(nint hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.GetWindowTextLength(hWnd) == 0)
            return false;
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
            return false;
        if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != nint.Zero && (exStyle & NativeMethods.WS_EX_APPWINDOW) == 0)
            return false;
        var cloaked = 0;
        return NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, ref cloaked, sizeof(int)) != 0 || cloaked == 0;
    }

    private static string GetTitle(nint hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length <= 0)
            return string.Empty;
        var buffer = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return string.Empty; // Exited meanwhile.
        }
    }

    private static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;
        public const uint GW_OWNER = 4;
        public const int DWMWA_CLOAKED = 14;
        public const int SW_RESTORE = 9;

        public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(nint hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(nint hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(nint hWnd, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(nint hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(nint hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(nint hWnd, int index);

        [DllImport("user32.dll")]
        public static extern nint GetWindow(nint hWnd, uint command);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(nint hWnd, int attribute, ref int value, int size);
    }
}
