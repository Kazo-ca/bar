namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Discovers and loads plugin implementations.
/// </summary>
public interface IPluginLoader
{
    /// <summary>Get all discovered plugins.</summary>
    IReadOnlyList<IPlugin> GetPlugins();

    /// <summary>Get a plugin by its unique ID.</summary>
    IPlugin? GetPlugin(string uniqueId);
}
