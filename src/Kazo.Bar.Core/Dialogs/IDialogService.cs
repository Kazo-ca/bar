namespace Kazo.Bar.Core.Dialogs;

/// <summary>
/// Opens a Razor component in a dialog. In the Windows (WPF) host each dialog is a separate
/// native window with its own WebView, sharing the application's singletons (settings, Git,
/// time tracking, <see cref="Events.EventAggregator"/>...), so a change made in the dialog is seen
/// at once by the toolbar, like two tabs of a Blazor Server app. Hosts without windows show the
/// component in an overlay inside the page instead.
///
/// The component receives a <see cref="DialogContext"/> cascading value to close itself and return data.
/// See doc/Dialogs.md.
/// </summary>
public interface IDialogService
{
    /// <summary>True when dialogs open as separate native windows.</summary>
    bool OpensWindows { get; }

    /// <summary>
    /// Opens <paramref name="componentType"/> with <paramref name="parameters"/> (component parameter
    /// names → values). If a dialog with the same key is open, it is brought to the front and returned.
    /// </summary>
    Task<IDialogReference> OpenAsync(Type componentType, IReadOnlyDictionary<string, object?>? parameters = null, DialogOptions? options = null);

    /// <summary>Closes the open dialog with this key, if any.</summary>
    Task CloseAsync(string key, DialogResult? result = null);

    /// <summary>True while a dialog with this key is open.</summary>
    bool IsOpen(string key);
}

/// <summary>Typed helpers.</summary>
public static class DialogServiceExtensions
{
    public static Task<IDialogReference> OpenAsync<TComponent>(this IDialogService dialogs,
        IReadOnlyDictionary<string, object?>? parameters = null, DialogOptions? options = null) =>
        dialogs.OpenAsync(typeof(TComponent), parameters, options);

    /// <summary>The key a dialog gets when <see cref="DialogOptions.Key"/> is not set.</summary>
    public static string KeyFor(Type componentType, DialogOptions? options) =>
        string.IsNullOrWhiteSpace(options?.Key) ? componentType.FullName ?? componentType.Name : options.Key;
}
