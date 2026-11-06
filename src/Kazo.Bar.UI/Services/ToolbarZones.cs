namespace Kazo.Bar.UI.Services;

using Kazo.Bar.Core.Interfaces;

/// <summary>
/// Which modules each toolbar zone holds, in default order (US3.7). Shared by the toolbar
/// and the Settings page so both list the same modules.
/// </summary>
public static class ToolbarZones
{
    public const string GitPluginId = "git-tools";
    public const string TimeTrackerPluginId = "time-tracker";

    /// <summary>Project pill, always first (with the Git module glued to it when Git is enabled).</summary>
    public const string ProjectModuleId = "core.project";

    /// <summary>Quick actions zone (not a plugin).</summary>
    public const string ActionsModuleId = "core.actions";

    /// <summary>Timer display (always shown; opens the Time Tracker when that plugin is enabled).</summary>
    public const string TimerModuleId = "core.timer";

    /// <summary>
    /// The last module of the toolbar (US3.9): it always ends the system zone, next to the
    /// settings button, and is the only module that cannot be moved.
    /// </summary>
    public const string PinnedModuleId = TimerModuleId;

    /// <summary>
    /// Modules that never move (US3.9): the project pill and the Git module glued to it on the
    /// left, and the timer, last module of the toolbar, on the right.
    /// </summary>
    public static bool IsPinned(string moduleId) =>
        moduleId is ProjectModuleId or GitPluginId or PinnedModuleId;

    /// <summary>Main zone (movable modules): the quick actions. The Git module is part of the project pill.</summary>
    public static IEnumerable<string> MainDefaults(IEnumerable<IPlugin> visiblePlugins) =>
        new[] { ActionsModuleId };

    /// <summary>Plugins shown as compact indicators in the system zone (Work Items, CI/CD...).</summary>
    public static IEnumerable<IPlugin> Indicators(IEnumerable<IPlugin> visiblePlugins) =>
        visiblePlugins.Where(p => p.IsEnabled && p is IPluginIndicator
                                  && p.UniqueId != GitPluginId && p.UniqueId != TimeTrackerPluginId);

    /// <summary>System zone: plugin indicators, then the timer.</summary>
    public static IEnumerable<string> SystemDefaults(IEnumerable<IPlugin> visiblePlugins) =>
        Indicators(visiblePlugins).Select(p => p.UniqueId).Append(TimerModuleId);

    /// <summary>Name of a module for menus and Settings.</summary>
    public static string LabelOf(string moduleId, IEnumerable<IPlugin> plugins) => moduleId switch
    {
        ActionsModuleId => "Quick actions",
        TimerModuleId => "Timer",
        _ => plugins.FirstOrDefault(p => p.UniqueId == moduleId)?.Name ?? moduleId,
    };
}
