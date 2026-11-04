namespace DevToolbar.UI.Services;

using DevToolbar.Core.Interfaces;

/// <summary>
/// Which modules each toolbar zone holds, in default order (US3.7). Shared by the toolbar
/// and the Settings page so both list the same modules.
/// </summary>
public static class ToolbarZones
{
    public const string GitPluginId = "git-tools";
    public const string TimeTrackerPluginId = "time-tracker";

    /// <summary>Quick actions zone (not a plugin).</summary>
    public const string ActionsModuleId = "core.actions";

    /// <summary>Timer display (always shown; opens the Time Tracker when that plugin is enabled).</summary>
    public const string TimerModuleId = "core.timer";

    /// <summary>Main zone: the Git module when enabled, then the quick actions.</summary>
    public static IEnumerable<string> MainDefaults(IEnumerable<IPlugin> visiblePlugins) =>
        (visiblePlugins.Any(p => p.UniqueId == GitPluginId && p.IsEnabled) ? new[] { GitPluginId } : Array.Empty<string>())
            .Append(ActionsModuleId);

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
