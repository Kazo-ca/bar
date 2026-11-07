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

    public bool IsOpen(string key) => Dispatcher.CheckAccess()
        ? _open.ContainsKey(key)
        : Dispatcher.Invoke(() => _open.ContainsKey(key));

    private static Dispatcher Dispatcher => Application.Current.Dispatcher;

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
            close: ctx => OnUiThread(() =>
            {
                // Closed by the component or the opener: close the window if it is still this dialog's.
                if (window != null && window.Context == ctx && !window.IsClosed) window.Close();
                return true;
            }),
            setTitle: title => _ = OnUiThread(() =>
            {
                if (window != null) window.Title = title;
                return true;
            }));

        window = new DialogWindow(services, context, componentType, parameters, options);
        _open[key] = window;
        window.Closed += (_, _) =>
        {
            if (_open.TryGetValue(key, out var current) && current == window) _open.Remove(key);
            // Window closed by the user (title bar ✕, Alt+F4) or by the app shutting down.
            context.Complete(DialogResult.Cancel);
        };
        Program.Log($"Dialog opened: {key} ({componentType.Name}).");
        window.Show();
        window.Activate();
        return context;
    }

    private static Task<T> OnUiThread<T>(Func<T> action) =>
        Dispatcher.CheckAccess() ? Task.FromResult(action()) : Dispatcher.InvokeAsync(action).Task;
}
