using Kazo.Bar.Core.Dialogs;
using Microsoft.AspNetCore.Components;

namespace Kazo.Bar.UI.Dialogs;

/// <summary>
/// <see cref="IDialogService"/> for hosts that cannot open native windows (Web test host, MAUI):
/// dialogs are shown in an overlay by <see cref="DialogOutlet"/>, or the host navigates to
/// <see cref="DialogOptions.FallbackRoute"/> when one is given. Register it as scoped (one per page/circuit).
/// </summary>
public sealed class InPageDialogService(NavigationManager navigation) : IDialogService
{
    private readonly List<InPageDialog> _open = new();
    private readonly object _gate = new();

    /// <summary>Raised when a dialog opens, closes or comes to the front.</summary>
    public event Action? Changed;

    public bool OpensWindows => false;

    /// <summary>Open dialogs, bottom to top.</summary>
    public IReadOnlyList<InPageDialog> Dialogs
    {
        get { lock (_gate) return _open.ToArray(); }
    }

    public Task<IDialogReference> OpenAsync(Type componentType, IReadOnlyDictionary<string, object?>? parameters = null, DialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        options ??= new DialogOptions();
        var key = DialogServiceExtensions.KeyFor(componentType, options);

        if (!string.IsNullOrWhiteSpace(options.FallbackRoute))
        {
            // The page replaces the toolbar: nothing to wait for, the result completes at once.
            navigation.NavigateTo(options.FallbackRoute);
            var navigated = new DialogContext(key, isWindow: false, options.Title, _ => Task.CompletedTask);
            navigated.Complete(DialogResult.Cancel);
            return Task.FromResult<IDialogReference>(navigated);
        }

        InPageDialog dialog;
        lock (_gate)
        {
            var existing = _open.FindIndex(d => d.Context.Key == key);
            if (existing >= 0)
            {
                // Single instance: bring it to the front.
                dialog = _open[existing];
                _open.RemoveAt(existing);
                _open.Add(dialog);
            }
            else
            {
                var context = new DialogContext(key, isWindow: false, options.Title, RemoveAsync, _ => Changed?.Invoke());
                dialog = new InPageDialog(context, componentType, ToComponentParameters(parameters), options);
                _open.Add(dialog);
            }
        }
        Changed?.Invoke();
        return Task.FromResult<IDialogReference>(dialog.Context);
    }

    public Task CloseAsync(string key, DialogResult? result = null)
    {
        InPageDialog? dialog;
        lock (_gate) dialog = _open.Find(d => d.Context.Key == key);
        return dialog?.Context.CloseAsync(result ?? DialogResult.Cancel) ?? Task.CompletedTask;
    }

    public bool IsOpen(string key)
    {
        lock (_gate) return _open.Exists(d => d.Context.Key == key);
    }

    private Task RemoveAsync(DialogContext context)
    {
        lock (_gate) _open.RemoveAll(d => d.Context == context);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>DynamicComponent takes non-null values; a null parameter value is passed as-is.</summary>
    internal static Dictionary<string, object> ToComponentParameters(IReadOnlyDictionary<string, object?>? parameters) =>
        parameters?.ToDictionary(p => p.Key, p => p.Value!) ?? new Dictionary<string, object>();
}

/// <summary>A dialog shown in the page overlay.</summary>
public sealed record InPageDialog(DialogContext Context, Type ComponentType, Dictionary<string, object> Parameters, DialogOptions Options);
