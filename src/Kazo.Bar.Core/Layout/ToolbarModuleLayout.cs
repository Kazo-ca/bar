namespace Kazo.Bar.Core.Layout;

/// <summary>
/// Pure logic for placing modules in the toolbar's zones (US3.7, US3.9): which zone holds each
/// module, in which order, and what a drag and drop does. The user's choices are one saved
/// order and one saved zone per module. One module can be pinned: it stays last in its default
/// zone, cannot be moved, and nothing can be dropped after it. Always returns new collections.
/// </summary>
public static class ToolbarModuleLayout
{
    public const string MainZone = "main";
    public const string SystemZone = "system";

    public static bool IsZone(string? zoneId) => zoneId is MainZone or SystemZone;

    /// <summary>
    /// Places the modules: each one goes in its saved zone (or its default zone), each zone is
    /// sorted by the saved order (<see cref="ToolbarModuleOrder.Arrange"/>), and the pinned
    /// module ends its default zone.
    /// </summary>
    public static ToolbarArrangement Arrange(
        IEnumerable<string> mainDefaults,
        IEnumerable<string> systemDefaults,
        IReadOnlyList<string>? savedOrder,
        IReadOnlyDictionary<string, string>? savedZones,
        string? pinnedLast = null)
    {
        ArgumentNullException.ThrowIfNull(mainDefaults);
        ArgumentNullException.ThrowIfNull(systemDefaults);
        var main = mainDefaults.Distinct(StringComparer.Ordinal).ToList();
        var system = systemDefaults.Where(id => !main.Contains(id, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
        var all = main.Concat(system).ToList();

        string ZoneFor(string id)
        {
            var defaultZone = main.Contains(id, StringComparer.Ordinal) ? MainZone : SystemZone;
            if (id == pinnedLast) return defaultZone;
            return savedZones != null && savedZones.TryGetValue(id, out var saved) && IsZone(saved) ? saved : defaultZone;
        }

        IReadOnlyList<string> Build(string zoneId)
        {
            var arranged = ToolbarModuleOrder.Arrange(all.Where(id => ZoneFor(id) == zoneId), savedOrder);
            return pinnedLast != null && arranged.Contains(pinnedLast, StringComparer.Ordinal)
                ? arranged.Where(id => id != pinnedLast).Append(pinnedLast).ToList()
                : arranged;
        }

        return new ToolbarArrangement(Build(MainZone), Build(SystemZone));
    }

    /// <summary>Whether the module can be dragged.</summary>
    public static bool CanDrag(ToolbarArrangement arrangement, string moduleId, string? pinnedLast) =>
        moduleId != pinnedLast && arrangement.ZoneOf(moduleId) != null;

    /// <summary>
    /// Moves a module to <paramref name="index"/> of <paramref name="zoneId"/> (index counted
    /// once the module left its old place). The index is clamped, and never goes past the
    /// pinned module. Unchanged when the module cannot move.
    /// </summary>
    public static ToolbarArrangement MoveTo(ToolbarArrangement arrangement, string moduleId, string zoneId, int index, string? pinnedLast)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        if (!IsZone(zoneId) || !CanDrag(arrangement, moduleId, pinnedLast)) return arrangement;

        var main = arrangement.Main.Where(id => id != moduleId).ToList();
        var system = arrangement.System.Where(id => id != moduleId).ToList();
        var target = zoneId == MainZone ? main : system;

        var limit = target.Count;
        var pinnedIndex = pinnedLast == null ? -1 : target.IndexOf(pinnedLast);
        if (pinnedIndex >= 0) limit = pinnedIndex;
        target.Insert(Math.Clamp(index, 0, limit), moduleId);

        return new ToolbarArrangement(main, system);
    }

    /// <summary>
    /// Drops a module on another one: it takes the target's place. In the same zone this works
    /// like a sortable list (dropped on a module to its right, it goes after it); from another
    /// zone it goes before the target.
    /// </summary>
    public static ToolbarArrangement DropOn(ToolbarArrangement arrangement, string moduleId, string targetId, string? pinnedLast)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        if (moduleId == targetId || arrangement.ZoneOf(targetId) is not { } zoneId) return arrangement;
        var index = IndexOf(arrangement.Zone(zoneId), targetId);
        return MoveTo(arrangement, moduleId, zoneId, index, pinnedLast);
    }

    /// <summary>Drops a module at the end of a zone (before the pinned module if it is there).</summary>
    public static ToolbarArrangement DropAtEnd(ToolbarArrangement arrangement, string moduleId, string zoneId, string? pinnedLast) =>
        MoveTo(arrangement, moduleId, zoneId, int.MaxValue, pinnedLast);

    /// <summary>
    /// Saved order for an arrangement: ids saved before that are not on the toolbar now
    /// (disabled plugins) keep their place in front, then the main zone, then the system zone.
    /// </summary>
    public static List<string> OrderOf(ToolbarArrangement arrangement, IReadOnlyList<string>? previousOrder)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        var shown = arrangement.Main.Concat(arrangement.System).ToList();
        return (previousOrder ?? Array.Empty<string>())
            .Where(id => !shown.Contains(id, StringComparer.Ordinal))
            .Concat(shown)
            .ToList();
    }

    /// <summary>Saved zones for an arrangement: previous entries are kept, shown modules get their current zone.</summary>
    public static Dictionary<string, string> ZonesOf(ToolbarArrangement arrangement, IReadOnlyDictionary<string, string>? previousZones)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        var zones = previousZones?.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal)
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in arrangement.Main) zones[id] = MainZone;
        foreach (var id in arrangement.System) zones[id] = SystemZone;
        return zones;
    }

    private static int IndexOf(IReadOnlyList<string> list, string id)
    {
        for (var i = 0; i < list.Count; i++)
            if (string.Equals(list[i], id, StringComparison.Ordinal)) return i;
        return -1;
    }
}
