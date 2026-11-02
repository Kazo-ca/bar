namespace DevToolbar.UI.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

/// <summary>
/// Closes a dropdown/popover when the user clicks outside its element or presses Escape.
/// Backed by DevToolbar.addDismissListener in toolbar-interop.js (a fixed-position CSS
/// backdrop cannot be used because the toolbar shell's backdrop-filter clips it).
/// </summary>
public sealed class DismissListener : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly Func<Task> _onDismiss;
    private DotNetObjectReference<DismissListener>? _selfRef;
    private int? _handle;

    public DismissListener(IJSRuntime js, Func<Task> onDismiss)
    {
        _js = js;
        _onDismiss = onDismiss;
    }

    /// <summary>Start listening for outside clicks / Escape relative to <paramref name="element"/>.</summary>
    public async Task AttachAsync(ElementReference element)
    {
        await DetachAsync();
        _selfRef ??= DotNetObjectReference.Create(this);
        try
        {
            _handle = await _js.InvokeAsync<int>("DevToolbar.addDismissListener", element, _selfRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            // Without the listener the explicit close buttons still work.
            _handle = null;
        }
    }

    /// <summary>Stop listening (safe to call when not attached).</summary>
    public async Task DetachAsync()
    {
        if (_handle is not { } handle) return;
        _handle = null;
        try
        {
            await _js.InvokeVoidAsync("DevToolbar.removeDismissListener", handle);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            // Circuit gone or page unloading — listener dies with the page.
        }
    }

    /// <summary>Invoked from JavaScript on outside click or Escape.</summary>
    [JSInvokable]
    public Task Dismiss()
    {
        _handle = null; // JS side already removed its listeners
        return _onDismiss();
    }

    public async ValueTask DisposeAsync()
    {
        await DetachAsync();
        _selfRef?.Dispose();
        _selfRef = null;
    }
}
