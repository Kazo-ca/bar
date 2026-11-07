namespace Kazo.Bar.Core.Dialogs;

/// <summary>
/// Given to the component shown in a dialog as a cascading value:
/// <c>[CascadingParameter] public DialogContext? Dialog { get; set; }</c>.
/// Null when the component is rendered normally (in the toolbar, or as a routed page).
/// </summary>
public sealed class DialogContext : IDialogReference
{
    private readonly TaskCompletionSource<DialogResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<DialogContext, Task> _close;
    private readonly Action<string>? _setTitle;

    public DialogContext(string key, bool isWindow, string? title, Func<DialogContext, Task> close, Action<string>? setTitle = null)
    {
        Key = key;
        IsWindow = isWindow;
        Title = title;
        _close = close;
        _setTitle = setTitle;
    }

    public string Key { get; }

    /// <summary>True in a separate native window, false in the in-page overlay.</summary>
    public bool IsWindow { get; }

    public string? Title { get; private set; }

    public Task<DialogResult> Result => _result.Task;

    public bool IsClosed => _result.Task.IsCompleted;

    /// <summary>Closes the dialog with <paramref name="data"/> for the opener.</summary>
    public Task CloseAsync(object? data) => CloseAsync(DialogResult.Ok(data));

    /// <summary>Closes the dialog as cancelled.</summary>
    public Task CancelAsync() => CloseAsync(DialogResult.Cancel);

    /// <inheritdoc />
    public async Task CloseAsync(DialogResult? result = null)
    {
        if (!Complete(result ?? DialogResult.Ok())) return;
        await _close(this);
    }

    /// <summary>Changes the window title (or overlay header) while the dialog is open.</summary>
    public void SetTitle(string title)
    {
        Title = title;
        _setTitle?.Invoke(title);
    }

    /// <summary>Host side: records the result once. Returns false if the dialog was already closed.</summary>
    public bool Complete(DialogResult result) => _result.TrySetResult(result);
}
