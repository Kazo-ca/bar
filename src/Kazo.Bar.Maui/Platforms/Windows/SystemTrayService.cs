namespace Kazo.Bar.Maui.Platforms.Windows;

/// <summary>
/// System Tray integration for Windows (US8.2).
/// Uses Windows notification area to keep Kazo Bar running in background.
/// </summary>
public class SystemTrayService : IDisposable
{
    private bool _isVisible;

    /// <summary>Event fired when the tray icon is double-clicked (restore).</summary>
    public event Action? OnRestoreRequested;

    /// <summary>Event fired when "Quit" is selected from tray menu.</summary>
    public event Action? OnQuitRequested;

    /// <summary>
    /// Initialize the system tray icon.
    /// In a real implementation, this would use H.NotifyIcon or similar library.
    /// </summary>
    public void Initialize()
    {
        // TODO: Use H.NotifyIcon NuGet package for tray icon support
        // var notifyIcon = new TaskbarIcon {
        //     ToolTipText = "Kazo Bar",
        //     IconSource = new BitmapImage(new Uri("pack://application:,,,/icon.ico")),
        // };
        _isVisible = true;
        System.Diagnostics.Debug.WriteLine("[SystemTray] Initialized");
    }

    /// <summary>Show the tray icon and hide the main window.</summary>
    public void Show()
    {
        _isVisible = true;
        System.Diagnostics.Debug.WriteLine("[SystemTray] Icon shown");
    }

    /// <summary>Hide the tray icon.</summary>
    public void Hide()
    {
        _isVisible = false;
        System.Diagnostics.Debug.WriteLine("[SystemTray] Icon hidden");
    }

    /// <summary>Update the tray icon badge (e.g., unread CI/CD count).</summary>
    public void UpdateBadge(int count)
    {
        System.Diagnostics.Debug.WriteLine($"[SystemTray] Badge updated: {count}");
    }

    public void Dispose()
    {
        _isVisible = false;
    }
}
