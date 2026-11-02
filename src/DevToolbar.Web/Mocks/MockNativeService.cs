namespace DevToolbar.Web.Mocks;

using DevToolbar.Core.Interfaces;

/// <summary>
/// Mock native service for web testing. All native operations are no-ops or logged.
/// </summary>
public class MockNativeService : INativeService
{
    public bool IsSessionLocked => false;

    public event Action<bool>? OnSessionLockChanged
    {
        add { }
        remove { }
    }

    public void ShowSystemNotification(string title, string message)
    {
        Console.WriteLine($"[MockNative] Notification: {title} - {message}");
    }

    public void SetTopMost(bool topMost)
    {
        Console.WriteLine($"[MockNative] TopMost set to {topMost}");
    }

    public void MinimizeToTray()
    {
        Console.WriteLine("[MockNative] Minimized to tray");
    }

    public void RestoreFromTray()
    {
        Console.WriteLine("[MockNative] Restored from tray");
    }

    /// <summary>Number of exit requests received (a browser tab cannot be closed by the server).</summary>
    public int ExitRequestCount { get; private set; }

    public void ExitApplication()
    {
        ExitRequestCount++;
        Console.WriteLine("[MockNative] Exit requested");
    }
}
