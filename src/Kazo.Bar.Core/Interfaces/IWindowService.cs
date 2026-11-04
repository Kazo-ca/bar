namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>
/// Service for listing open windows and activating them.
/// Used by the active windows module (US8.5).
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Gets the list of visible, top-level application windows on a specific monitor.
    /// </summary>
    /// <param name="monitorIndex">Zero-based monitor index (0 = primary).</param>
    /// <returns>List of visible windows on the specified monitor.</returns>
    Task<List<WindowInfo>> GetOpenWindowsOnMonitorAsync(int monitorIndex);

    /// <summary>
    /// Activates (brings to foreground) a window by its handle.
    /// Safe to call even if the window no longer exists.
    /// </summary>
    /// <param name="hWnd">Window handle.</param>
    void ActivateWindow(nint hWnd);

    /// <summary>
    /// Gets information about all connected monitors.
    /// </summary>
    /// <returns>List of monitor information ordered by index.</returns>
    List<MonitorInfo> GetMonitors();
}
