namespace Kazo.Bar.Core.Layout;

/// <summary>
/// Pure logic for the order of toolbar modules (US3.7). The toolbar has zones (main zone:
/// Git, quick actions, open windows; system zone: plugin indicators and timer). The user's
/// order is one saved list of ids; each zone sorts its own modules by it. Modules missing
/// from the saved list keep their default order after the saved ones. Always returns new lists.
/// </summary>
public static class ToolbarModuleOrder
{
    /// <summary>The zone's modules sorted by the saved order, then the unsaved ones in default order.</summary>
    public static IReadOnlyList<string> Arrange(IEnumerable<string> defaultOrder, IReadOnlyList<string>? saved)
    {
        ArgumentNullException.ThrowIfNull(defaultOrder);
        var ids = defaultOrder.Distinct(StringComparer.Ordinal).ToList();
        if (saved is not { Count: > 0 }) return ids;

        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < saved.Count; i++)
            rank.TryAdd(saved[i], i);

        return ids
            .Select((id, index) => (id, index))
            .OrderBy(x => rank.TryGetValue(x.id, out var r) ? r : int.MaxValue)
            .ThenBy(x => x.index)
            .Select(x => x.id)
            .ToList();
    }

    /// <summary>Whether <paramref name="id"/> can move by <paramref name="offset"/> inside its arranged zone.</summary>
    public static bool CanMove(IReadOnlyList<string> arrangedZone, string id, int offset)
    {
        var index = IndexOf(arrangedZone, id);
        var target = index + offset;
        return index >= 0 && offset != 0 && target >= 0 && target < arrangedZone.Count;
    }

    /// <summary>
    /// Moves a module inside its zone and returns the new saved order: ids of other zones are
    /// kept, the zone's ids follow in their new order. Unchanged when the move is not possible.
    /// </summary>
    public static List<string> Move(IReadOnlyList<string> arrangedZone, IReadOnlyList<string>? saved, string id, int offset)
    {
        ArgumentNullException.ThrowIfNull(arrangedZone);
        var current = saved?.ToList() ?? new List<string>();
        if (!CanMove(arrangedZone, id, offset)) return current;

        var zone = arrangedZone.ToList();
        var index = zone.IndexOf(id);
        zone.RemoveAt(index);
        zone.Insert(index + offset, id);

        return current.Where(x => !zone.Contains(x, StringComparer.Ordinal)).Concat(zone).ToList();
    }

    private static int IndexOf(IReadOnlyList<string> list, string id)
    {
        for (var i = 0; i < list.Count; i++)
            if (string.Equals(list[i], id, StringComparison.Ordinal)) return i;
        return -1;
    }
}
