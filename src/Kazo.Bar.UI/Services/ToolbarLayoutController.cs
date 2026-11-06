namespace Kazo.Bar.UI.Services;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Layout;
using Kazo.Bar.Core.Models;

/// <summary>
/// Reads and saves the user's toolbar layout (US3.6 widths, US3.7 order, US3.9 zones) in the
/// global settings. Created by the toolbar layout and cascaded to every <c>ModuleFrame</c>.
/// Also runs the "✏️ Réorganiser" mode (US3.9): drops change a draft that is only saved by
/// <see cref="CommitReorganizeAsync"/> and dropped by <see cref="CancelReorganize"/>.
/// Every change builds new lists and dictionaries (no in-place mutation of the saved ones).
/// </summary>
public sealed class ToolbarLayoutController
{
    private readonly IGlobalSettingsService _settings;

    /// <summary>Order and zones being edited in reorganize mode; null outside of it.</summary>
    private ToolbarLayoutSettings? _draft;

    /// <summary>Arrangement the toolbar shows now, used to resolve drops.</summary>
    private ToolbarArrangement _shown = ToolbarArrangement.Empty;

    public ToolbarLayoutController(IGlobalSettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Raised after the order, a zone, a width or the reorganize state changed.</summary>
    public event Action? Changed;

    /// <summary>The module that always stays last on the toolbar and cannot be moved (US3.9).</summary>
    public string PinnedModuleId => ToolbarZones.PinnedModuleId;

    private ToolbarLayoutSettings Saved => _settings.Current.ToolbarLayout ?? new ToolbarLayoutSettings();

    /// <summary>Order and zones the toolbar shows: the draft while reorganizing, else the saved ones.</summary>
    private ToolbarLayoutSettings Effective => _draft ?? Saved;

    /// <summary>A single zone's modules in the user's order (ignores zone changes).</summary>
    public IReadOnlyList<string> Arrange(IEnumerable<string> zoneDefaultOrder) =>
        ToolbarModuleOrder.Arrange(zoneDefaultOrder, Effective.ModuleOrder);

    /// <summary>Both zones with the user's zones and order (the draft while reorganizing).</summary>
    public ToolbarArrangement Arrange(IEnumerable<string> mainDefaults, IEnumerable<string> systemDefaults)
    {
        var layout = Effective;
        _shown = ToolbarModuleLayout.Arrange(mainDefaults, systemDefaults, layout.ModuleOrder, layout.ModuleZones, PinnedModuleId);
        return _shown;
    }

    /// <summary>Width the user gave a module, or null.</summary>
    public int? SavedWidth(string moduleId) =>
        Saved.ModuleWidths.TryGetValue(moduleId, out var width) ? width : null;

    public bool HasCustomOrder => Saved.ModuleOrder.Count > 0 || Saved.ModuleZones.Count > 0;

    public bool HasCustomWidths => Saved.ModuleWidths.Count > 0;

    // ── Reorganize mode (US3.9) ──

    public bool IsReorganizing => _draft != null;

    /// <summary>Module being dragged, or null.</summary>
    public string? DraggedModuleId { get; private set; }

    /// <summary>Module or zone end (<see cref="ZoneTargetId"/>) under the dragged module, for the drop marker.</summary>
    public string? DropTargetId { get; private set; }

    /// <summary>True when the draft differs from what is saved.</summary>
    public bool HasPendingChanges { get; private set; }

    /// <summary>Whether the module never moves: project pill, Git module, timer (US3.9).</summary>
    public bool IsPinned(string moduleId) => ToolbarZones.IsPinned(moduleId);

    public bool CanDrag(string moduleId) => IsReorganizing && !IsPinned(moduleId) && _shown.ZoneOf(moduleId) != null;

    /// <summary>Id used as drop target for the end of a zone.</summary>
    public static string ZoneTargetId(string zoneId) => $"zone:{zoneId}";

    public void BeginReorganize()
    {
        if (IsReorganizing) return;
        _draft = Copy(Saved);
        HasPendingChanges = false;
        Changed?.Invoke();
    }

    public void CancelReorganize()
    {
        if (!IsReorganizing) return;
        _draft = null;
        ClearDrag();
        HasPendingChanges = false;
        Changed?.Invoke();
    }

    public async Task CommitReorganizeAsync()
    {
        if (_draft is not { } draft) return;
        var pending = HasPendingChanges;
        _draft = null;
        ClearDrag();
        HasPendingChanges = false;
        if (!pending)
        {
            Changed?.Invoke();
            return;
        }
        await UpdateAsync(l => new ToolbarLayoutSettings
        {
            ModuleOrder = draft.ModuleOrder.ToList(),
            ModuleZones = new Dictionary<string, string>(draft.ModuleZones),
            ModuleWidths = new Dictionary<string, int>(l.ModuleWidths),
        });
    }

    public void StartDrag(string moduleId)
    {
        if (!CanDrag(moduleId)) return;
        DraggedModuleId = moduleId;
        DropTargetId = null;
        Changed?.Invoke();
    }

    public void DragOver(string targetId)
    {
        if (DraggedModuleId == null) return;
        var target = targetId == DraggedModuleId ? null : targetId;
        if (DropTargetId == target) return;
        DropTargetId = target;
        Changed?.Invoke();
    }

    public void EndDrag()
    {
        if (DraggedModuleId == null && DropTargetId == null) return;
        ClearDrag();
        Changed?.Invoke();
    }

    /// <summary>Drops the dragged module on another module: it takes that module's place.</summary>
    public void DropOn(string targetId) =>
        ApplyDrop(dragged => ToolbarModuleLayout.DropOn(_shown, dragged, targetId, PinnedModuleId));

    /// <summary>Drops the dragged module at the end of a zone.</summary>
    public void DropAtEnd(string zoneId) =>
        ApplyDrop(dragged => ToolbarModuleLayout.DropAtEnd(_shown, dragged, zoneId, PinnedModuleId));

    private void ApplyDrop(Func<string, ToolbarArrangement> drop)
    {
        if (_draft is not { } draft || DraggedModuleId is not { } dragged)
        {
            EndDrag();
            return;
        }
        var next = drop(dragged);
        if (!next.SequenceEqual(_shown))
        {
            _draft = new ToolbarLayoutSettings
            {
                ModuleOrder = ToolbarModuleLayout.OrderOf(next, draft.ModuleOrder),
                ModuleZones = ToolbarModuleLayout.ZonesOf(next, draft.ModuleZones),
                ModuleWidths = new Dictionary<string, int>(draft.ModuleWidths),
            };
            _shown = next;
            HasPendingChanges = true;
        }
        ClearDrag();
        Changed?.Invoke();
    }

    private void ClearDrag()
    {
        DraggedModuleId = null;
        DropTargetId = null;
    }

    // ── Saved changes ──

    public Task SetWidthAsync(string moduleId, PluginLayout layout, int width)
    {
        if (!layout.Resizable) return Task.CompletedTask;
        var clamped = layout.EffectiveWidth(width);
        return UpdateAsync(l => Copy(l, widths: new Dictionary<string, int>(l.ModuleWidths) { [moduleId] = clamped }));
    }

    public Task ResetWidthAsync(string moduleId) => UpdateAsync(l =>
        Copy(l, widths: l.ModuleWidths.Where(kv => kv.Key != moduleId).ToDictionary(kv => kv.Key, kv => kv.Value)));

    public Task MoveAsync(IReadOnlyList<string> arrangedZone, string moduleId, int offset)
    {
        if (!CanMove(arrangedZone, moduleId, offset)) return Task.CompletedTask;
        return UpdateAsync(l => Copy(l, order: ToolbarModuleOrder.Move(arrangedZone, l.ModuleOrder, moduleId, offset)));
    }

    /// <summary>Whether "Move left / right" (or ▲ / ▼) can move the module: never the pinned one, never past it.</summary>
    public bool CanMove(IReadOnlyList<string> arrangedZone, string moduleId, int offset)
    {
        if (IsPinned(moduleId) || !ToolbarModuleOrder.CanMove(arrangedZone, moduleId, offset)) return false;
        var index = arrangedZone.ToList().IndexOf(moduleId);
        return !IsPinned(arrangedZone[index + offset]);
    }

    /// <summary>Default order and zones for every module (widths are kept).</summary>
    public Task ResetOrderAsync() => UpdateAsync(l => new ToolbarLayoutSettings
    {
        ModuleWidths = new Dictionary<string, int>(l.ModuleWidths),
    });

    public Task ResetWidthsAsync() => UpdateAsync(l => Copy(l, widths: new Dictionary<string, int>()));

    private static ToolbarLayoutSettings Copy(ToolbarLayoutSettings l, List<string>? order = null, Dictionary<string, int>? widths = null) => new()
    {
        ModuleOrder = order ?? l.ModuleOrder.ToList(),
        ModuleZones = new Dictionary<string, string>(l.ModuleZones),
        ModuleWidths = widths ?? new Dictionary<string, int>(l.ModuleWidths),
    };

    private async Task UpdateAsync(Func<ToolbarLayoutSettings, ToolbarLayoutSettings> change)
    {
        await _settings.UpdateAsync(s => s.ToolbarLayout = change(s.ToolbarLayout ?? new ToolbarLayoutSettings()));
        Changed?.Invoke();
    }
}
