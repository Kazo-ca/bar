# Dialogs and multiple windows

Any Razor component can be opened in a **dialog**. In the Windows (WPF) host a dialog is a separate,
normal window (title bar, resizable, taskbar button) with its own WebView. Every window is built on the
**same service provider**, so all singletons are shared: settings, Git, time tracking, plugins,
`EventAggregator`... A change made in a dialog is seen by the toolbar at once, and the other way round,
like two tabs of a Blazor Server app sharing one server.

Hosts that cannot open windows (the Web test host, the frozen MAUI host) show the same component in an
overlay inside the page, so the same code works everywhere and stays testable with Playwright.

Used today by **Settings** (⚙️ button and tray menu) and the **Time Report** (📊 button).

## Opening a dialog

```razor
@using Kazo.Bar.Core.Dialogs
@inject IDialogService Dialogs

<button @onclick="OpenCommit">Commit…</button>

@code {
    private async Task OpenCommit()
    {
        var dialog = await Dialogs.OpenAsync<GitManagerPanel>(
            new Dictionary<string, object?> { [nameof(GitManagerPanel.RepositoryPath)] = _repositoryPath },
            new DialogOptions { Title = "Commit", Width = 1000, Height = 700, Key = $"git:{_repositoryPath}" });

        // Optional: wait for the dialog to close and read what it returned.
        var result = await dialog.Result;
        if (!result.Cancelled && result.GetData<string>() is { } sha) { /* ... */ }
    }
}
```

| API (`Kazo.Bar.Core.Dialogs`) | What it does |
|---|---|
| `IDialogService.OpenAsync(type, parameters, options)` / `OpenAsync<T>(...)` | Opens the component with these parameters (parameter name → value). Returns an `IDialogReference`. |
| `IDialogService.OpensWindows` | True in the WPF host (real windows), false where dialogs are overlays. |
| `IDialogService.CloseAsync(key, result)` / `IsOpen(key)` | Close or test a dialog by key from anywhere. |
| `IDialogReference.Result` | `Task<DialogResult>` completed when the dialog closes: `Cancelled` (✕, Esc, window closed) or `Data`. |
| `DialogOptions.Title` | Window title (or overlay header). |
| `DialogOptions.Width` / `Height` | Initial size in DIPs, kept inside the screen. Default 900 × 650. |
| `DialogOptions.Key` | **Single instance**: opening a key that is already open brings that window to the front. Defaults to the component type, so there is one Settings window. Give a distinct key to allow one dialog per repository, per work item... |
| `DialogOptions.FallbackRoute` | Hosts without windows navigate to this route instead of showing an overlay (Settings uses `/settings`). |
| `DialogOptions.InPageFrame` | Overlay only: `false` when the component draws its own modal (the Time Report). Default `true`: a frame with title and ✕. |

The application's own dialogs have helpers in `Kazo.Bar.UI.Dialogs.KnownDialogs`:
`Dialogs.OpenSettingsAsync()` and `Dialogs.OpenTimeReportAsync()`.

## Inside the dialog

The component receives a cascading `DialogContext`. It is `null` when the same component is rendered
normally (in the toolbar, or as a routed page), so one component can serve both.

```razor
@code {
    [CascadingParameter] public DialogContext? Dialog { get; set; }

    private Task Commit() => Dialog?.CloseAsync(_newSha) ?? Task.CompletedTask;   // returns data
    private Task Cancel() => Dialog?.CancelAsync() ?? Task.CompletedTask;
    // Dialog.IsWindow: true in its own window, false in the overlay.
    // Dialog.SetTitle("Commit — main"): changes the window title while open.
}
```

Rules for components shown in a window:

- **Fill the window and scroll inside it.** The window root (`.dialog-window`) is `100vh` with
  `overflow: auto`. Do not rely on the toolbar's transparent-window rules (`wpf-host.css`).
- **`HostSizedRegion` is fine**: inside a dialog it no longer asks the toolbar window to grow.
- **No navigation**: a dialog window has no router. Use component state, not `NavigationManager`.
- **Toasts** (`INotificationService`) appear on the toolbar, which stays visible.
- The active theme is applied (`data-theme` and CSS variables on `.dialog-window`).

## Messages between windows

Because the windows share singletons, any shared service is already a channel. For explicit messages,
use the `EventAggregator` singleton (`Kazo.Bar.Core.Events`): publish a record in one window, receive it
in the others. It is thread-safe; `Subscribe` returns an `IDisposable`.

```razor
@implements IDisposable
@inject EventAggregator Events

@code {
    public sealed record CommitCreated(string RepositoryPath, string Sha);   // in a shared assembly

    private IDisposable? _subscription;

    protected override void OnInitialized() =>
        // Handlers run on the publisher's thread: update the UI through InvokeAsync.
        _subscription = Events.Subscribe<CommitCreated>(e => InvokeAsync(() => { _lastSha = e.Sha; StateHasChanged(); }));

    // In the dialog:  Events.Publish(new CommitCreated(path, sha));

    public void Dispose() => _subscription?.Dispose();
}
```

Services that raise their own events (`ISettingsService.OnActiveProjectChanged`,
`IGlobalSettingsService.OnSettingsChanged`, `ITimeTrackingService.OnTrackingChanged`, `IPlugin.OnStateChanged`)
work the same way: each window that subscribes is notified.

## How it works

| Piece | Where | Role |
|---|---|---|
| `IDialogService`, `DialogOptions`, `DialogContext`, `DialogResult` | `src/Kazo.Bar.Core/Dialogs` | Contract, no UI dependency. |
| `WpfDialogService`, `DialogWindow` | `src/Kazo.Bar.Windows` | One WPF window per key, `BlazorWebView` with `Services` = the root provider, host page `wwwroot/dialog.html`, root component `DialogWindowRoot`. Singleton. |
| `DialogWindowRoot` | `src/Kazo.Bar.UI/Dialogs` | Applies the theme, cascades `DialogContext`, renders the component with `DynamicComponent`. |
| `InPageDialogService`, `DialogOutlet` | `src/Kazo.Bar.UI/Dialogs` | Overlay fallback. Scoped (one per page/circuit); `DialogOutlet` is in the Web layout and is a second root component of the MAUI page. |

Each WebView has its own DI scope, so scoped services (`IJSRuntime`, `NavigationManager`) belong to their
window, while singletons are shared. Closing the toolbar quits the app and closes every dialog.
