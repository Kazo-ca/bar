using System.Windows;
using DevToolbar.Core.Models;
using Microsoft.Web.WebView2.Core;

namespace DevToolbar.Windows.Services;

/// <summary>
/// Bridge between WPF-specific services and the toolbar window.
/// Replaces the MAUI lookups (Application.Current.Windows / WindowNative) used by the MAUI host.
/// </summary>
public sealed class ToolbarHost
{
    private CoreWebView2? _core;
    private string? _pendingBodyClassScript;

    public Window? Window { get; private set; }
    public nint Hwnd { get; private set; }

    /// <summary>Logical size of the web surface (DIPs); grown by <see cref="WpfHostSurfaceService"/>.</summary>
    public SurfaceSize Surface { get; internal set; } = new(ToolbarWindow.SurfaceWidth, ToolbarWindow.SurfaceHeight);

    /// <summary>True when docked at the bottom: the window keeps its bottom edge when resized.</summary>
    public bool AnchorBottom { get; private set; }

    /// <summary>True while the host resizes its own window, so popups are not dismissed as if the user moved it.</summary>
    public bool IsAdjustingSurface { get; internal set; }

    internal void Attach(Window window, nint hwnd)
    {
        Window = window;
        Hwnd = hwnd;
    }

    internal void AttachWebView(CoreWebView2 core)
    {
        _core = core;
        core.Settings.IsStatusBarEnabled = false;
        core.ProcessFailed += (_, e) => Program.Log($"WebView2 process failed: {e.ProcessFailedKind} ({e.Reason}), exit {e.ExitCode}");
        // Page-side errors (unhandled JS errors, Blazor failures) are forwarded through the console.
        _ = core.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
        core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown").DevToolsProtocolEventReceived +=
            (_, e) => Program.Log($"JS exception: {e.ParameterObjectAsJson}");
        core.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled").DevToolsProtocolEventReceived += (_, e) =>
        {
            if (e.ParameterObjectAsJson.Contains("\"type\":\"error\"") || e.ParameterObjectAsJson.Contains("\"type\":\"warning\""))
                Program.Log($"JS console: {e.ParameterObjectAsJson}");
        };
        core.NavigationCompleted += (_, _) =>
        {
            if (_pendingBodyClassScript != null) _ = _core.ExecuteScriptAsync(_pendingBodyClassScript);
        };
    }

    /// <summary>Navigates the Blazor router (e.g. "/settings") from native code such as the tray menu.</summary>
    public void NavigateTo(string path)
    {
        if (_core == null) return;
        var escaped = System.Text.Json.JsonSerializer.Serialize(path);
        _ = _core.ExecuteScriptAsync($"window.Blazor && Blazor.navigateTo({escaped})");
    }

    /// <summary>Toggles a CSS class on &lt;body&gt; (e.g. "dock-bottom"), now or once the page has loaded.</summary>
    public void SetBodyClass(string className, bool enabled)
    {
        if (className == "dock-bottom") AnchorBottom = enabled;
        var script = $"document.body && document.body.classList.toggle('{className}', {(enabled ? "true" : "false")})";
        _pendingBodyClassScript = script;
        if (_core != null) _ = _core.ExecuteScriptAsync(script);
    }
}
