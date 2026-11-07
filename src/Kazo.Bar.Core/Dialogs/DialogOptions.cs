namespace Kazo.Bar.Core.Dialogs;

/// <summary>How a dialog is shown. Every value is optional.</summary>
public sealed record DialogOptions
{
    /// <summary>Default size of a dialog window, in DIPs (logical pixels).</summary>
    public const double DefaultWidth = 900;
    public const double DefaultHeight = 650;

    /// <summary>Window title (title bar and taskbar), or header of the in-page frame.</summary>
    public string? Title { get; init; }

    /// <summary>Initial width of the window (DIPs). The window is resizable and never larger than the screen.</summary>
    public double Width { get; init; } = DefaultWidth;

    /// <summary>Initial height of the window (DIPs).</summary>
    public double Height { get; init; } = DefaultHeight;

    /// <summary>
    /// Single-instance key: opening a dialog whose key is already open brings that dialog
    /// to the front instead of opening a second one. Defaults to the component's full type name,
    /// so there is one Settings window, one Time Report window, and so on. Use a distinct key
    /// (e.g. "git-commit:C:\repo") to allow one dialog per repository.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Hosts that cannot open windows (Web test host, MAUI) navigate to this route instead of
    /// showing the in-page overlay. Use it for components that are also routable pages ("/settings").
    /// </summary>
    public string? FallbackRoute { get; init; }

    /// <summary>
    /// In-page overlay only: false when the component draws its own overlay and close button
    /// (the Time Report), true (default) to get the standard frame with a title bar and ✕.
    /// </summary>
    public bool InPageFrame { get; init; } = true;
}
