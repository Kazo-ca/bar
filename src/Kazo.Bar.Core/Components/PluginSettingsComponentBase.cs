namespace Kazo.Bar.Core.Components;

using Kazo.Bar.Core.Models;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Base class for plugin settings panels hosted in the plugin configuration window.
/// </summary>
public abstract class PluginSettingsComponentBase : ComponentBase
{
    /// <summary>The project whose plugin settings are being edited.</summary>
    [Parameter, EditorRequired]
    public ProjectConfig Project { get; set; } = default!;

    /// <summary>
    /// Invoke after changing <see cref="Project"/> so the host persists it
    /// and refreshes the toolbar.
    /// </summary>
    [Parameter]
    public EventCallback<ProjectConfig> OnProjectChanged { get; set; }
}
