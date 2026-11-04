namespace Kazo.Bar.UI.Services;

using Kazo.Bar.Core.Interfaces;

/// <summary>
/// In-memory notification service for displaying toasts.
/// </summary>
public class NotificationService : INotificationService
{
    public event Action<ToastNotification>? OnNotification;

    public void ShowInfo(string message, string? title = null) =>
        Notify(message, title, ToastLevel.Info);

    public void ShowSuccess(string message, string? title = null) =>
        Notify(message, title, ToastLevel.Success);

    public void ShowError(string message, string? title = null) =>
        Notify(message, title, ToastLevel.Error);

    public void ShowWarning(string message, string? title = null) =>
        Notify(message, title, ToastLevel.Warning);

    private void Notify(string message, string? title, ToastLevel level)
    {
        OnNotification?.Invoke(new ToastNotification
        {
            Message = message,
            Title = title,
            Level = level
        });
    }
}
