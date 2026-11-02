namespace DevToolbar.Core.Interfaces;

/// <summary>
/// Optional contract for plugins that expose their own settings panel in the
/// Settings → Plugins configuration window.
/// </summary>
public interface IConfigurablePlugin : IPlugin
{
    /// <summary>
    /// Razor component rendering the plugin's settings. It must derive from
    /// <see cref="DevToolbar.Core.Components.PluginSettingsComponentBase"/> so the
    /// configuration window can pass it the active project and a change callback.
    /// </summary>
    Type SettingsComponentType { get; }
}
