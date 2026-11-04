namespace Kazo.Bar.Core.Models;

/// <summary>
/// How a plugin's module takes room in the toolbar (US3.6). A plugin declares it through
/// <see cref="Interfaces.IPlugin.Layout"/>; the default is a fixed module sized by its content.
/// </summary>
public sealed record PluginLayout
{
    /// <summary>Fixed module: as wide as its content, no resize grip.</summary>
    public static readonly PluginLayout Fixed = new();

    /// <summary>True when the user can drag the module's right edge to change its width.</summary>
    public bool Resizable { get; init; }

    /// <summary>Width in CSS px used until the user resizes, and after "Reset width".</summary>
    public int DefaultWidth { get; init; }

    /// <summary>Narrowest width the user can drag to, and the one the module shrinks to when room runs out.</summary>
    public int MinWidth { get; init; }

    /// <summary>Widest width the user can drag to.</summary>
    public int MaxWidth { get; init; }

    /// <summary>A resizable module. Arguments are swapped or raised so that min ≤ default ≤ max.</summary>
    public static PluginLayout ResizableWidth(int defaultWidth, int minWidth, int maxWidth)
    {
        var min = Math.Max(1, Math.Min(minWidth, maxWidth));
        var max = Math.Max(minWidth, maxWidth);
        return new PluginLayout
        {
            Resizable = true,
            MinWidth = min,
            MaxWidth = max,
            DefaultWidth = Math.Clamp(defaultWidth, min, max),
        };
    }

    /// <summary>The width to use for a saved value: clamped, or the default when nothing is saved.</summary>
    public int EffectiveWidth(int? savedWidth) =>
        savedWidth is { } width && width > 0 ? Math.Clamp(width, MinWidth, MaxWidth) : DefaultWidth;
}
