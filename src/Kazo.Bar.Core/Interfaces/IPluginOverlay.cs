namespace Kazo.Bar.Core.Interfaces;

using Microsoft.AspNetCore.Components;

/// <summary>
/// Optional contract for a plugin that shows a window over the toolbar (for example the Git
/// plugin's repository manager, US5.7). The layout renders the fragment of every visible plugin
/// outside the toolbar, so the window is not clipped by it; the fragment renders nothing while
/// the plugin's window is closed.
/// </summary>
public interface IPluginOverlay
{
    RenderFragment? RenderOverlay();
}
