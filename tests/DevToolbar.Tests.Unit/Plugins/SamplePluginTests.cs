namespace DevToolbar.Tests.Unit.Plugins;

using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using DevToolbar.Plugins.Services;
using DevToolbar.UI.Services;
using Microsoft.AspNetCore.Components;

/// <summary>
/// US2.1: the example of doc/Writing-a-plugin.md, copied verbatim, still compiles and lands where
/// the guide says. Update both together.
/// </summary>
[TestFixture]
public class SamplePluginTests
{
    [Test]
    public async Task Indicator_FollowsTheActiveProject()
    {
        var plugin = new ActionCountPlugin();
        var changes = 0;
        plugin.OnStateChanged += () => changes++;
        var project = new ProjectConfig
        {
            Name = "Alpha",
            Actions = [new ActionConfig { Label = "Build", Icon = "🔨" }, new ActionConfig { Label = "Test", Icon = "🧪" }],
        };

        await plugin.OnProjectChangedAsync(new PluginContext { Project = project });

        Assert.That(plugin.IndicatorText, Is.EqualTo("2"));
        Assert.That(plugin.IndicatorTooltip, Is.EqualTo("Alpha: 2 action(s)"));
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(plugin.Render(), Is.Not.Null);
    }

    [Test]
    public void RegisteredPlugin_IsListed_AndShownInTheSystemZone()
    {
        var loader = new PluginLoader([new ActionCountPlugin()]);

        Assert.That(loader.GetPlugin("action-count"), Is.Not.Null);
        Assert.That(ToolbarZones.SystemDefaults(loader.GetPlugins()), Does.Contain("action-count"));
    }

    [Test]
    public void PluginWithoutIndicator_HasNoPlaceInTheToolbar()
    {
        var plugins = new IPlugin[] { new NoIndicatorPlugin() };

        Assert.That(ToolbarZones.SystemDefaults(plugins), Does.Not.Contain("no-indicator"));
        Assert.That(ToolbarZones.MainDefaults(plugins), Does.Not.Contain("no-indicator"));
    }

    private sealed class NoIndicatorPlugin : IPlugin
    {
        public string UniqueId => "no-indicator";
        public string Name => "No indicator";
        public string Icon => "?";
        public bool IsEnabled { get; set; } = true;
        public event Action? OnStateChanged { add { } remove { } }
        public Task OnProjectChangedAsync(PluginContext context) => Task.CompletedTask;
        public RenderFragment? Render() => null;
    }
}

// ----- Copied from doc/Writing-a-plugin.md ("Example") -----
public sealed class ActionCountPlugin : IPlugin, IPluginIndicator
{
    private ProjectConfig _project = new();

    public string UniqueId => "action-count";
    public string Name => "Action count";
    public string Icon => "🔢";
    public bool IsEnabled { get; set; } = true;
    public event Action? OnStateChanged;

    public string? IndicatorText => _project.Actions.Count.ToString();
    public string? IndicatorTooltip => $"{_project.Name}: {_project.Actions.Count} action(s)";
    public int BadgeCount => 0;
    public string? StatusLevel => null;

    public Task OnProjectChangedAsync(PluginContext context)
    {
        _project = context.Project;
        OnStateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public RenderFragment? Render() => builder =>
    {
        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", "action-count-list");
        var sequence = 2;
        foreach (var action in _project.Actions)
        {
            builder.OpenElement(sequence++, "li");
            builder.AddContent(sequence++, $"{action.Icon} {action.Label}");
            builder.CloseElement();
        }
        builder.CloseElement();
    };
}
