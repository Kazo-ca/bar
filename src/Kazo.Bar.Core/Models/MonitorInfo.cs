namespace Kazo.Bar.Core.Models;

/// <summary>
/// Represents information about a display monitor.
/// </summary>
public class MonitorInfo
{
    /// <summary>Zero-based index of this monitor.</summary>
    public int Index { get; set; }

    /// <summary>Display name (e.g., "\\.\DISPLAY1").</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Whether this is the primary monitor.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Full bounds of the monitor in pixels.</summary>
    public ScreenRect Bounds { get; set; } = new();

    /// <summary>Working area (excluding taskbar) in pixels.</summary>
    public ScreenRect WorkArea { get; set; } = new();

    /// <summary>Friendly display label, e.g., "Monitor 1 (Primary)".</summary>
    public string DisplayLabel =>
        IsPrimary ? $"Monitor {Index + 1} (Primary)" : $"Monitor {Index + 1}";
}

/// <summary>
/// Simple rectangle for screen coordinates.
/// </summary>
public class ScreenRect
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }

    public int Width => Right - Left;
    public int Height => Bottom - Top;
}
