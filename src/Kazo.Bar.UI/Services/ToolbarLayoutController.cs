namespace Kazo.Bar.UI.Services;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Layout;
using Kazo.Bar.Core.Models;

/// <summary>
/// Reads and saves the user's toolbar layout (US3.6 widths, US3.7 order) in the global
/// settings. Created by the toolbar layout and cascaded to every <c>ModuleFrame</c>.
/// Every change builds new lists and dictionaries (no in-place mutation of the saved ones).
/// </summary>
public sealed class ToolbarLayoutController
{
    private readonly IGlobalSettingsService _settings;

    public ToolbarLayoutController(IGlobalSettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Raised after the order or a width changed.</summary>
    public event Action? Changed;

    private ToolbarLayoutSettings Saved => _settings.Current.ToolbarLayout ?? new ToolbarLayoutSettings();

    /// <summary>A zone's modules in the user's order.</summary>
    public IReadOnlyList<string> Arrange(IEnumerable<string> zoneDefaultOrder) =>
        ToolbarModuleOrder.Arrange(zoneDefaultOrder, Saved.ModuleOrder);

    /// <summary>Width the user gave a module, or null.</summary>
    public int? SavedWidth(string moduleId) =>
        Saved.ModuleWidths.TryGetValue(moduleId, out var width) ? width : null;

    public bool HasCustomOrder => Saved.ModuleOrder.Count > 0;

    public bool HasCustomWidths => Saved.ModuleWidths.Count > 0;

    public Task SetWidthAsync(string moduleId, PluginLayout layout, int width)
    {
        if (!layout.Resizable) return Task.CompletedTask;
        var clamped = layout.EffectiveWidth(width);
        return UpdateAsync(l => new ToolbarLayoutSettings
        {
            ModuleOrder = l.ModuleOrder.ToList(),
            ModuleWidths = new Dictionary<string, int>(l.ModuleWidths) { [moduleId] = clamped },
        });
    }

    public Task ResetWidthAsync(string moduleId) => UpdateAsync(l => new ToolbarLayoutSettings
    {
        ModuleOrder = l.ModuleOrder.ToList(),
        ModuleWidths = l.ModuleWidths.Where(kv => kv.Key != moduleId).ToDictionary(kv => kv.Key, kv => kv.Value),
    });

    public Task MoveAsync(IReadOnlyList<string> arrangedZone, string moduleId, int offset) => UpdateAsync(l => new ToolbarLayoutSettings
    {
        ModuleOrder = ToolbarModuleOrder.Move(arrangedZone, l.ModuleOrder, moduleId, offset),
        ModuleWidths = new Dictionary<string, int>(l.ModuleWidths),
    });

    public Task ResetOrderAsync() => UpdateAsync(l => new ToolbarLayoutSettings
    {
        ModuleWidths = new Dictionary<string, int>(l.ModuleWidths),
    });

    public Task ResetWidthsAsync() => UpdateAsync(l => new ToolbarLayoutSettings
    {
        ModuleOrder = l.ModuleOrder.ToList(),
    });

    private async Task UpdateAsync(Func<ToolbarLayoutSettings, ToolbarLayoutSettings> change)
    {
        await _settings.UpdateAsync(s => s.ToolbarLayout = change(s.ToolbarLayout ?? new ToolbarLayoutSettings()));
        Changed?.Invoke();
    }
}
