namespace Kazo.Bar.Core.Models;

/// <summary>
/// Defines how the toolbar positions itself on screen.
/// </summary>
public enum ToolbarBehavior
{
    /// <summary>Docked at the top of the target monitor, reserves screen space (AppBar).</summary>
    DockedTop,

    /// <summary>Docked at the bottom of the target monitor, reserves screen space (AppBar).</summary>
    DockedBottom,

    /// <summary>Floating freely, can be moved by the user.</summary>
    Floating,

    /// <summary>Overlay mode — always on top but does not reserve screen space.</summary>
    Overlay
}
