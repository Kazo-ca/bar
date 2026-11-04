namespace Kazo.Bar.UI.Services;

/// <summary>Why open popups (dropdowns, menus) are asked to close.</summary>
public enum PopupDismissReason
{
    /// <summary>Pointer pressed inside the page but outside every popup root.</summary>
    OutsideClick,

    /// <summary>Escape pressed: close and give focus back to the trigger.</summary>
    Escape,

    /// <summary>The host window lost activation to another application.</summary>
    HostDeactivated,

    /// <summary>The host window moved or changed DPI.</summary>
    HostMoved
}

/// <summary>
/// Shared, host-agnostic channel used to close popups.
/// Popups subscribe; the page (outside click / Escape) and the native host
/// (deactivation, move) publish. No invisible overlay is used to catch the
/// "outside" click, so the first click outside the toolbar is never consumed.
/// </summary>
public sealed class PopupDismissService
{
    public event Action<PopupDismissRequest>? DismissRequested;

    public void RequestDismiss(PopupDismissReason reason, string? sourceRoot = null)
        => DismissRequested?.Invoke(new PopupDismissRequest(reason, sourceRoot));
}

/// <summary>
/// A dismissal request. <paramref name="SourceRoot"/> is the <c>data-popup-root</c>
/// name the pointer went down in, if any; that popup keeps itself open.
/// </summary>
public readonly record struct PopupDismissRequest(PopupDismissReason Reason, string? SourceRoot)
{
    public bool AppliesTo(string root) => Reason != PopupDismissReason.OutsideClick || SourceRoot != root;
}
