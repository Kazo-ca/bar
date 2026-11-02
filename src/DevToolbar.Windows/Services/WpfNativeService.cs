using DevToolbar.Core.Interfaces;

namespace DevToolbar.Windows.Services;

/// <summary>INativeService for the WPF host (MAUI-free equivalent of WindowsNativeService).</summary>
public sealed class WpfNativeService(ToolbarHost host) : INativeService
{
    public bool IsSessionLocked => false;

    public event Action<bool>? OnSessionLockChanged { add { } remove { } }

    public void ShowSystemNotification(string title, string message)
        => System.Diagnostics.Debug.WriteLine($"[Notification] {title}: {message}");

    public void SetTopMost(bool topMost) => OnUi(w => w.Topmost = topMost);

    public void MinimizeToTray() => OnUi(w => w.Hide());

    public void RestoreFromTray() => OnUi(w => { w.Show(); w.Activate(); });

    private void OnUi(Action<System.Windows.Window> action)
    {
        var window = host.Window;
        if (window == null) return;
        if (window.Dispatcher.CheckAccess()) action(window);
        else window.Dispatcher.Invoke(() => action(window));
    }
}
