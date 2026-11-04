namespace Kazo.Bar.Plugins.Services;

using Kazo.Bar.Core.Interfaces;

/// <summary>
/// Discovers and manages plugin instances.
/// </summary>
public class PluginLoader : IPluginLoader
{
    private readonly List<IPlugin> _plugins;

    public PluginLoader(IEnumerable<IPlugin> plugins)
    {
        _plugins = plugins.ToList();
    }

    public IReadOnlyList<IPlugin> GetPlugins() => _plugins.AsReadOnly();

    public IPlugin? GetPlugin(string uniqueId) =>
        _plugins.FirstOrDefault(p => p.UniqueId == uniqueId);
}
