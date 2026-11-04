namespace Kazo.Bar.UI.Components;

/// <summary>
/// How many quick actions fit in the room left by the other zones (US3.4). Widths of the
/// actions are measured in the page; one not measured yet counts as a round 38 px button.
/// When they do not all fit, room is kept for the "…" button.
/// </summary>
public static class ActionOverflow
{
    public const int ButtonWidth = 38;
    public const int Gap = 6;

    public readonly record struct Layout(int Visible, int Hidden);

    /// <param name="count">Number of actions.</param>
    /// <param name="availableWidth">Room for the actions in CSS px; null when not measured yet.</param>
    /// <param name="widthOf">Measured width of an action by index, or null when unknown.</param>
    public static Layout Fit(int count, int? availableWidth, Func<int, int?>? widthOf = null)
    {
        if (count <= 0) return new Layout(0, 0);
        if (availableWidth is not { } room) return new Layout(count, 0);

        int Width(int i) => widthOf?.Invoke(i) ?? ButtonWidth;

        var all = Enumerable.Range(0, count).Sum(Width) + Gap * (count - 1);
        if (all <= room) return new Layout(count, 0);

        // Keep the "…" button, then add actions in order while they fit before it.
        var used = ButtonWidth;
        var visible = 0;
        while (visible < count && used + Width(visible) + Gap <= room)
        {
            used += Width(visible) + Gap;
            visible++;
        }
        return new Layout(visible, count - visible);
    }
}
