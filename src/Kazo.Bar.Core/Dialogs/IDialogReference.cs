namespace Kazo.Bar.Core.Dialogs;

/// <summary>An open dialog, as seen by the code that opened it.</summary>
public interface IDialogReference
{
    /// <summary>Single-instance key (see <see cref="DialogOptions.Key"/>).</summary>
    string Key { get; }

    /// <summary>Completes once, when the dialog closes.</summary>
    Task<DialogResult> Result { get; }

    /// <summary>Closes the dialog from the opener's side (no-op if already closed).</summary>
    Task CloseAsync(DialogResult? result = null);
}
