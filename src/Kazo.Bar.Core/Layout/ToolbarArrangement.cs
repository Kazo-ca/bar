namespace Kazo.Bar.Core.Layout;

/// <summary>
/// The modules of each movable toolbar zone, in display order (US3.7, US3.9). Immutable:
/// every change returns a new arrangement.
/// </summary>
public sealed record ToolbarArrangement(IReadOnlyList<string> Main, IReadOnlyList<string> System)
{
    public static readonly ToolbarArrangement Empty = new(Array.Empty<string>(), Array.Empty<string>());

    /// <summary>A zone's modules ("main" or "system"); empty for an unknown zone.</summary>
    public IReadOnlyList<string> Zone(string zoneId) => zoneId switch
    {
        ToolbarModuleLayout.MainZone => Main,
        ToolbarModuleLayout.SystemZone => System,
        _ => Array.Empty<string>(),
    };

    /// <summary>Zone that holds the module, or null when it is not on the toolbar.</summary>
    public string? ZoneOf(string moduleId) =>
        Main.Contains(moduleId, StringComparer.Ordinal) ? ToolbarModuleLayout.MainZone
        : System.Contains(moduleId, StringComparer.Ordinal) ? ToolbarModuleLayout.SystemZone
        : null;

    public bool SequenceEqual(ToolbarArrangement other) =>
        Main.SequenceEqual(other.Main, StringComparer.Ordinal) && System.SequenceEqual(other.System, StringComparer.Ordinal);
}
