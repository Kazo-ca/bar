namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Interface that all toolbar plugins must implement.
/// </summary>
public interface IPlugin
{
    /// <summary>Unique identifier for the plugin.</summary>
    string UniqueId { get; }

    /// <summary>Display name of the plugin.</summary>
    string Name { get; }

    /// <summary>Icon CSS class (e.g. a Material Icon name).</summary>
    string Icon { get; }

    /// <summary>Whether the plugin is currently enabled.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Called when the active project changes.</summary>
    Task OnProjectChangedAsync(PluginContext context);

    /// <summary>Returns a RenderFragment to display in the plugin zone.</summary>
    RenderFragment? Render();

    /// <summary>
    /// How the plugin's module takes room in the toolbar (US3.6). Return
    /// <see cref="PluginLayout.ResizableWidth"/> to let the user drag its width; right-clicking
    /// the module then offers "Reset width". Fixed (sized by content) by default.
    /// </summary>
    PluginLayout Layout => PluginLayout.Fixed;

    /// <summary>Event fired when the plugin's internal state changes and needs re-rendering.</summary>
    event Action? OnStateChanged;
}
