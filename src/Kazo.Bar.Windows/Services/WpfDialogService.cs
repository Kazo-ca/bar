using System.Windows;
using System.Windows.Threading;
using Kazo.Bar.Core.Dialogs;

namespace Kazo.Bar.Windows.Services;

/// <summary>
/// Opens each dialog in its own native window (<see cref="DialogWindow"/>), with a BlazorWebView
/// built on the application's root service provider: singletons (settings, Git, time tracking,
/// EventAggregator...) are the same instances as in the toolbar, so both windows see each other's
/// changes at once. One window per <see cref="DialogOptions.Key"/>; opening it again brings it to the front.
/// </summary>
public sealed class WpfDialogService(IServiceProvider services) : IDialogService
{
    /// <summary>Open windows by key. Touched on the UI thread only.</summary>
    private readonly Dictionary<string, DialogWindow> _open = new(StringComparer.Ordinal);

    public bool OpensWindows => true;

    public Task<IDialogReference> OpenAsync(Type componentType, IReadOnlyDictionary<string, object?>? parameters = null, DialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        return OnUiThread(() => Open(componentType, parameters, options ?? new DialogOptions()));
    }

    public async Task CloseAsync(string key, DialogResult? result = null)
    {
        var context = await OnUiThread(() => _open.TryGetValue(key, out var window) ? window.Context : null);
        if (context != null) await context.CloseAsync(result ?? DialogResult.Cancel);
    }

    public bool IsOpen(string key)
    {
        var dispatcher = Dispatcher;
        if (dispatcher == null) return false;
        return dispatcher.CheckAccess() ? _open.ContainsKey(key) : dispatcher.Invoke(() => _open.ContainsKey(key));
    }

    /// <summary>Null once the application has shut down.</summary>
    private static Dispatcher? Dispatcher => Application.Current?.Dispatcher;

    private IDialogReference Open(Type componentType, IReadOnlyDictionary<string, object?>? parameters, DialogOptions options)
    {
        var key = DialogServiceExtensions.KeyFor(componentType, options);
        if (_open.TryGetValue(key, out var existing))
        {
            existing.BringToFront();
            return existing.Context;
        }

        DialogWindow? window = null;
        var context = new DialogContext(key, isWindow: true, options.Title,
            close: ctx =>
            {
                // Posted, never inline: the request usually comes from a Blazor event handler of
                // this very window, which must finish before its WebView is torn down.
                Dispatcher?.BeginInvoke(() =>
                {
                    if (window != null && window.Context == ctx && !window.IsClosed) window.Close();
                });
                return Task.CompletedTask;
            },
            setTitle: title => Dispatcher?.BeginInvoke(() =>
            {
                if (window != null && !window.IsClosed) window.Title = title;
            }));

        window = new DialogWindow(services, context, componentType, parameters, options);
        window.Closed += (_, _) =>
        {
            if (_open.TryGetValue(key, out var current) && current == window) _open.Remove(key);
            // Window closed by the user (title bar ✕, Alt+F4) or by the app shutting down.
            context.Complete(DialogResult.Cancel);
        };
        window.Show();
        // Registered only once shown: a window that failed to open is never "brought to front" later.
        _open[key] = window;
        window.Activate();
        Program.Log($"Dialog opened: {key} ({componentType.Name}).");
        return context;
    }

    /// <summary>Runs on the UI thread; failures come back as a faulted task, never as a synchronous throw.</summary>
    private static Task<T> OnUiThread<T>(Func<T> action)
    {
        var dispatcher = Dispatcher;
        if (dispatcher == null)
            return Task.FromException<T>(new InvalidOperationException("The application has shut down."));
        if (!dispatcher.CheckAccess()) return dispatcher.InvokeAsync(action).Task;
        try
        {
            return Task.FromResult(action());
        }
        catch (Exception ex)
        {
            Program.Log($"Dialog failed: {ex}");
            return Task.FromException<T>(ex);
        }
    }
}
