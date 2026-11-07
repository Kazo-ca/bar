namespace Kazo.Bar.Core.Dialogs;

/// <summary>How a dialog ended: closed with optional data, or cancelled (✕, Esc, window closed).</summary>
public sealed record DialogResult(bool Cancelled, object? Data)
{
    public static DialogResult Cancel { get; } = new(true, null);

    public static DialogResult Ok(object? data = null) => new(false, data);

    /// <summary>The data as <typeparamref name="T"/>, or default when cancelled or of another type.</summary>
    public T? GetData<T>() => !Cancelled && Data is T value ? value : default;
}
