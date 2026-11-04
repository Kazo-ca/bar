namespace Kazo.Bar.Core.Layout;

/// <summary>
/// How many modules of the system zone stay in the toolbar when room runs out (US3.8, rule 5).
/// Modules keep the user's order (US3.7); the last ones go first into the "»" menu. The room is
/// what is left once the other zones are at their smallest (quick actions reduced to "…",
/// resizable modules at their minimum width), so modules only move when nothing else can shrink.
/// </summary>
public static class ModuleOverflow
{
    /// <param name="room">Room for the system zone's modules in CSS px; null when not measured yet (everything shown).</param>
    /// <param name="widths">Width of each module, in zone order.</param>
    /// <param name="gap">Space between two items of the zone.</param>
    /// <param name="buttonWidth">Width of the "»" button.</param>
    /// <returns>Number of modules shown in the toolbar; the others are in the menu.</returns>
    public static int VisibleCount(int? room, IReadOnlyList<int> widths, int gap, int buttonWidth)
    {
        ArgumentNullException.ThrowIfNull(widths);
        if (room is not { } available || widths.Count == 0) return widths.Count;

        var all = widths.Sum(w => Math.Max(0, w) + gap);
        if (all <= available) return widths.Count;

        var used = buttonWidth + gap;
        var visible = 0;
        while (visible < widths.Count && used + Math.Max(0, widths[visible]) + gap <= available)
        {
            used += Math.Max(0, widths[visible]) + gap;
            visible++;
        }
        return visible;
    }
}
