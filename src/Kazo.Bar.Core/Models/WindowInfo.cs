namespace Kazo.Bar.Core.Models;

/// <summary>
/// Represents a visible window on the desktop.
/// Used by the active windows module (US8.5) to show open applications.
/// </summary>
public class WindowInfo
{
    /// <summary>Window handle (HWND).</summary>
    public nint Hwnd { get; set; }

    /// <summary>Window title text.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Base64-encoded PNG icon of the window/application.</summary>
    public string IconBase64 { get; set; } = string.Empty;

    /// <summary>Name of the process owning this window.</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Whether this window is currently the foreground (focused) window.</summary>
    public bool IsActive { get; set; }
}
