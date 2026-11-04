namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Optional contract for plugins that show a compact indicator in the toolbar
/// (short label, status colour and/or numeric badge) and open their full
/// <see cref="IPlugin.Render"/> content in a popover when clicked.
/// </summary>
public interface IPluginIndicator
{
    /// <summary>Short text displayed next to the plugin icon (e.g. "#1234"). Null for icon only.</summary>
    string? IndicatorText { get; }

    /// <summary>Tooltip for the indicator button (e.g. "#1234 - Fix login redirect").</summary>
    string? IndicatorTooltip { get; }

    /// <summary>Number shown in a badge on the indicator. 0 hides the badge.</summary>
    int BadgeCount { get; }

    /// <summary>
    /// Global status of the plugin, used to colour the indicator:
    /// "success", "failure", "running" or null for neutral.
    /// </summary>
    string? StatusLevel { get; }
}
