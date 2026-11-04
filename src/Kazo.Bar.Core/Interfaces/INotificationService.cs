namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Service for displaying toast notifications to the user.
/// </summary>
public interface INotificationService
{
    /// <summary>Show an info notification.</summary>
    void ShowInfo(string message, string? title = null);

    /// <summary>Show a success notification.</summary>
    void ShowSuccess(string message, string? title = null);

    /// <summary>Show an error notification.</summary>
    void ShowError(string message, string? title = null);

    /// <summary>Show a warning notification.</summary>
    void ShowWarning(string message, string? title = null);

    /// <summary>Event fired when a new notification arrives.</summary>
    event Action<ToastNotification>? OnNotification;
}

/// <summary>
/// Represents a toast notification.
/// </summary>
public class ToastNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; } = string.Empty;
    public string? Title { get; set; }
    public ToastLevel Level { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Severity level of a toast notification.
/// </summary>
public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}
