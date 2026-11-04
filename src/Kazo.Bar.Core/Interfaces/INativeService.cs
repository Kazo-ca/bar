namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Abstracts native OS operations (notifications, window management, session events).
/// Implemented in MAUI with real Win32 calls, mocked in Web.
/// </summary>
public interface INativeService
{
    /// <summary>Show a system notification (toast/balloon).</summary>
    void ShowSystemNotification(string title, string message);

    /// <summary>Set the main window as always-on-top.</summary>
    void SetTopMost(bool topMost);

    /// <summary>Minimize the window to the system tray.</summary>
    void MinimizeToTray();

    /// <summary>Restore the window from the system tray.</summary>
    void RestoreFromTray();

    /// <summary>
    /// Quit the whole application (close the toolbar, remove the tray icon, release docking).
    /// Called from the Settings page "Quit Kazo Bar" action.
    /// </summary>
    void ExitApplication();

    /// <summary>Get whether the user session is locked.</summary>
    bool IsSessionLocked { get; }

    /// <summary>Event fired when the user session is locked or unlocked.</summary>
    event Action<bool>? OnSessionLockChanged;
}
