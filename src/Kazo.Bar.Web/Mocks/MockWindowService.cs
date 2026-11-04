using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

namespace Kazo.Bar.Web.Mocks;

/// <summary>
/// Mock IWindowService for web testing — returns a fixed set of sample windows
/// so the "Show Active Windows" taskbar module can be exercised in the browser.
/// </summary>
public class MockWindowService : IWindowService
{
    private readonly object _lock = new();
    private nint _activeHwnd = 101;

    private static readonly (nint Hwnd, string Title, string Process)[] SampleWindows =
    {
        (101, "MyWebAPI - Microsoft Visual Studio", "devenv"),
        (102, "Postman", "Postman"),
        (103, "localhost:5280 - Google Chrome", "chrome"),
    };

    public Task<List<WindowInfo>> GetOpenWindowsOnMonitorAsync(int monitorIndex)
    {
        lock (_lock)
        {
            var windows = SampleWindows
                .Select(w => new WindowInfo { Hwnd = w.Hwnd, Title = w.Title, ProcessName = w.Process, IsActive = w.Hwnd == _activeHwnd })
                .ToList();
            return Task.FromResult(windows);
        }
    }

    public void ActivateWindow(nint hWnd)
    {
        lock (_lock)
        {
            if (SampleWindows.Any(w => w.Hwnd == hWnd))
            {
                _activeHwnd = hWnd;
            }
        }
    }

    public List<MonitorInfo> GetMonitors()
        => new()
        {
            new MonitorInfo
            {
                Index = 0,
                DeviceName = "DISPLAY1",
                IsPrimary = true,
                Bounds = new ScreenRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
                WorkArea = new ScreenRect { Left = 0, Top = 0, Right = 1920, Bottom = 1040 }
            }
        };
}
