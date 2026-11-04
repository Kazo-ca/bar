namespace Kazo.Bar.Core.Models;

/// <summary>
/// Context information passed to plugins when the active project changes.
/// </summary>
public class PluginContext
{
    /// <summary>The active project configuration.</summary>
    public ProjectConfig Project { get; set; } = new();
}
