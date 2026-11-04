# Writing a plugin

A DevToolbar plugin is a .NET class that implements `IPlugin` and is registered in the host's
dependency injection. There is no loading of external DLLs yet (see US2.1 in [Features.md](Features.md)):
a plugin is compiled with the host, usually in `src/Kazo.Bar.Plugins` or in your own class library
referenced by the host.

## The contract

| Interface | Required | What it gives the plugin |
|---|---|---|
| `IPlugin` (`Kazo.Bar.Core.Interfaces`) | Yes | Identity, project changes, the popover content, the module layout. |
| `IPluginIndicator` | To appear in the toolbar | A compact indicator in the system zone: icon, short text, badge, status colour. Clicking it opens the popover with `Render()`. |
| `IConfigurablePlugin` | No | A settings panel in the plugin configuration window (Settings → Plugins → ⚙). |

> A plugin that implements only `IPlugin` is listed in Settings but has no place in the toolbar.
> Only the built-in Git module and the timer are placed without `IPluginIndicator`.

### `IPlugin`

| Member | Notes |
|---|---|
| `UniqueId` | Stable id, lowercase with dashes (`"my-tool"`). It is stored in `config.json` (`enabledPlugins`, `toolbarLayout`): never change it once released. |
| `Name`, `Icon` | Shown in Settings, menus and the indicator. `Icon` is a short text, usually an emoji. |
| `IsEnabled` | Global switch; the toolbar also requires the id in the active project's `enabledPlugins`. |
| `OnProjectChangedAsync(PluginContext)` | Called for **every** plugin when the active project changes, and once at start. `context.Project` is the `ProjectConfig` (repositories with `localPath`, actions, theme…). Reload your data here; cancel any polling started for the previous project. |
| `Render()` | Content of the popover. Return a `RenderFragment` (build it with `RenderTreeBuilder`, or return a Razor component, see below). |
| `Layout` | Optional. `PluginLayout.Fixed` (default) or `PluginLayout.ResizableWidth(default, min, max)` to let the user drag the module's width (US3.6). A resizable module is a CSS container named `module`, so its stylesheet can hide details with `@container module (max-width: …)` (US3.8). |
| `OnStateChanged` | Raise it when the indicator or the popover must re-render. It can be raised from any thread. |

### `IPluginIndicator`

| Member | Notes |
|---|---|
| `IndicatorText` | Short text next to the icon (`"#1234"`), or null for the icon only. Long texts are truncated. |
| `IndicatorTooltip` | Full text for the tooltip and the popover title. |
| `BadgeCount` | Number in a badge; 0 hides it. |
| `StatusLevel` | `"success"`, `"failure"`, `"running"` or null (neutral). |

### `IConfigurablePlugin`

`SettingsComponentType` is a Razor component that derives from
`Kazo.Bar.Core.Components.PluginSettingsComponentBase`. The window passes it:

- `Project`: the project being edited;
- `OnProjectChanged`: invoke it after changing `Project`; the host saves `config.json` and refreshes the toolbar.

## Where a plugin keeps its data

| Data | Where |
|---|---|
| Per-project or per-repository settings | In the project (`ProjectConfig`, `GitRepository`), saved through `OnProjectChanged`. Adding an optional property does not need a schema version change; renaming or removing one needs a migration (US2.5). |
| Tokens and passwords | **Never** in `config.json`. Store a `secretRef` in the configuration and the secret in `ISecretStore` (Credential Manager or DPAPI file, US2.4). The `SecretField` component (`Kazo.Bar.Plugins.Shared`) does the masked input. |
| History, caches, read states | `IStorageService` (`JsonStorageService`): one file `data\<namespace>.json` per namespace, with a `schemaVersion`. Use your plugin id as namespace. |
| Messages to the user | `INotificationService` (toasts). Catch provider errors and show them instead of throwing from `OnProjectChangedAsync` or `Render`. |

## Register the plugin

Add it to each host's dependency injection, next to the built-in plugins:

```csharp
// src/Kazo.Bar.Windows/Program.cs (real services)
services.AddSingleton<IPlugin, BuildCounterPlugin>();

// src/Kazo.Bar.Web/Program.cs (test host used by Playwright; register mocks for its services)
builder.Services.AddSingleton<IPlugin, BuildCounterPlugin>();
```

`PluginLoader` lists every registered `IPlugin`. The user then turns the plugin on per project in
Settings → Plugins (this writes its id to `enabledPlugins`) and can move its module with a right click
(US3.7).

## Example

A plugin that counts the project's actions and shows them in its popover. The same code is compiled and
checked by `tests/Kazo.Bar.Tests.Unit/Plugins/SamplePluginTests.cs`.

```csharp
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Microsoft.AspNetCore.Components;

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
```

To render a Razor component instead of building the tree by hand:

```csharp
public RenderFragment? Render() => builder =>
{
    builder.OpenComponent<ActionCountPanel>(0);
    builder.AddAttribute(1, nameof(ActionCountPanel.Project), _project);
    builder.CloseComponent();
};
```

Styles go in the plugin's own stylesheet; prefix every class with the plugin id (`.action-count-…`) so
they never clash with the toolbar or other plugins.

## Checklist

- [ ] `UniqueId` is stable and unique.
- [ ] `IPluginIndicator` is implemented if the plugin must appear in the toolbar.
- [ ] `OnProjectChangedAsync` reloads data and stops work started for the previous project.
- [ ] Errors are caught and shown with `INotificationService`; the toolbar never crashes because of a plugin.
- [ ] No secret in the configuration: only a `secretRef`.
- [ ] Registered in `Kazo.Bar.Windows` and `Kazo.Bar.Web` (with mocks), with unit tests and, for
      visible behaviour, a Playwright test on the Web host.
