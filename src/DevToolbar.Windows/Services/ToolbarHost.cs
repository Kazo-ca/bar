using System.Windows;
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

    internal void Attach(Window window, nint hwnd)
    {
        Window = window;
        Hwnd = hwnd;
    }

    internal void AttachWebView(CoreWebView2 core)
    {
        _core = core;
        core.Settings.IsStatusBarEnabled = false;
        core.NavigationCompleted += (_, _) =>
        {
            if (_pendingBodyClassScript != null) _ = _core.ExecuteScriptAsync(_pendingBodyClassScript);
        };
    }

    /// <summary>Toggles a CSS class on &lt;body&gt; (e.g. "dock-bottom"), now or once the page has loaded.</summary>
    public void SetBodyClass(string className, bool enabled)
    {
        var script = $"document.body && document.body.classList.toggle('{className}', {(enabled ? "true" : "false")})";
        _pendingBodyClassScript = script;
        if (_core != null) _ = _core.ExecuteScriptAsync(script);
    }
}
