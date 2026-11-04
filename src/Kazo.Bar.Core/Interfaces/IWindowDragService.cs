namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Optional host service letting the user move the toolbar window with a drag handle.
/// Only desktop hosts that own a movable window register it; the handle is hidden otherwise.
/// </summary>
public interface IWindowDragService
{
    /// <summary>False while docked (AppBar) or when the host cannot move its window.</summary>
    bool CanDrag { get; }

    /// <summary>
    /// Starts a native window move while the primary button is held; returns when released.
    /// </summary>
    Task DragAsync();

    /// <summary>
    /// Moves the window, if needed, so the given rectangle (CSS px, relative to the
    /// window's top-left) is fully inside the work area of the window's monitor.
    /// </summary>
    void KeepInWorkArea(double left, double top, double right, double bottom);
}
