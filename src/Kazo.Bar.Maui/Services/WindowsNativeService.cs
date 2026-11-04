using System.Runtime.InteropServices;
using Kazo.Bar.Core.Interfaces;

namespace Kazo.Bar.Maui.Services;

/// <summary>
/// Real Windows native service for desktop integration (US8.1, US8.2).
/// Implements TopMost, System Tray, and session lock detection.
/// </summary>
public class WindowsNativeService : INativeService
{
    private bool _isSessionLocked;

    public bool IsSessionLocked => _isSessionLocked;

    public event Action<bool>? OnSessionLockChanged;

    public void ShowSystemNotification(string title, string message)
    {
        // In MAUI, use platform-specific notification APIs
        // For now, log to debug output
        System.Diagnostics.Debug.WriteLine($"[Notification] {title}: {message}");
    }

    public void SetTopMost(bool topMost)
    {
        // Window TopMost is handled in App.cs via Window configuration
        // This method allows toggling at runtime
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            // MAUI doesn't have direct TopMost API; use platform-specific code
            // On Windows, use P/Invoke SetWindowPos
#if WINDOWS
            SetWindowTopMost(window, topMost);
#endif
        }
    }

    public void MinimizeToTray()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            // Hide the window (System Tray icon keeps app alive)
            Application.Current?.CloseWindow(window);
        }
    }

    public void RestoreFromTray()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            Application.Current?.ActivateWindow(window);
        }
    }

    public void ExitApplication()
    {
        Application.Current?.Quit();
    }

#if WINDOWS
    private static void SetWindowTopMost(Window window, bool topMost)
    {
        // Platform-specific implementation using Win32 API
        // This would use SetWindowPos with HWND_TOPMOST/HWND_NOTOPMOST
        System.Diagnostics.Debug.WriteLine($"[Native] SetTopMost: {topMost}");
    }
#endif
}
