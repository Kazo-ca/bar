namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>
/// Service for docking the toolbar window to a screen edge (AppBar behavior)
/// and managing its position across multiple monitors.
/// </summary>
public interface IDockingService
{
    /// <summary>
    /// Docks the toolbar window to the specified edge of the target monitor.
    /// Registers as a Windows AppBar to reserve screen space.
    /// </summary>
    /// <param name="edge">Top or Bottom edge.</param>
    /// <param name="monitorIndex">Zero-based monitor index.</param>
    /// <param name="heightPixels">Height of the toolbar in pixels.</param>
    void DockWindow(DockEdge edge, int monitorIndex, int heightPixels);

    /// <summary>
    /// Removes the AppBar registration and frees reserved screen space.
    /// </summary>
    void Undock();

    /// <summary>
    /// Moves the toolbar to the specified monitor in overlay mode
    /// (always on top, no screen space reservation).
    /// </summary>
    /// <param name="edge">Top or Bottom positioning.</param>
    /// <param name="monitorIndex">Zero-based monitor index.</param>
    /// <param name="heightPixels">Height of the toolbar in pixels.</param>
    void PositionOnMonitor(DockEdge edge, int monitorIndex, int heightPixels);

    /// <summary>Whether the toolbar is currently registered as an AppBar.</summary>
    bool IsDocked { get; }

    /// <summary>Applies the full toolbar behavior from GlobalSettings.</summary>
    /// <param name="settings">The current global settings.</param>
    void ApplySettings(GlobalSettings settings);
}
