using System.Runtime.InteropServices;
using DevToolbar.Core.Interfaces;
using Microsoft.Win32;

namespace DevToolbar.Windows.Services;

/// <summary>
/// Presence for the time tracker (US5.3): <see cref="SystemEvents.SessionSwitch"/> (lock, unlock,
/// remote disconnect), <see cref="SystemEvents.PowerModeChanged"/> (sleep, wake) and
/// <c>GetLastInputInfo</c> for keyboard and mouse inactivity in any application.
/// </summary>
internal sealed partial class WindowsUserPresenceService : IUserPresenceService, IDisposable
{
    private readonly object _gate = new();
    private bool _sessionLocked;
    private bool _asleep;

    public WindowsUserPresenceService()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    /// <summary>Locked (or disconnected), or asleep.</summary>
    public bool IsLocked { get { lock (_gate) return _sessionLocked || _asleep; } }
    public event Action? SessionLocked;
    public event Action? SessionUnlocked;

    public TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;
        // Both are milliseconds since boot on 32 bits; unsigned subtraction handles the 49-day wrap.
        var idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                Change(() => _sessionLocked = true);
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
            case SessionSwitchReason.SessionLogon:
                Change(() => _sessionLocked = false);
                break;
        }
    }

    // Sleep counts as away. When Windows asks for a password on wake, the session lock comes
    // before the sleep, so waking up is not a return: the unlock is.
    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
            Change(() => _asleep = true);
        else if (e.Mode == PowerModes.Resume)
            Change(() => _asleep = false);
    }

    private void Change(Action update)
    {
        bool before, after;
        lock (_gate)
        {
            before = _sessionLocked || _asleep;
            update();
            after = _sessionLocked || _asleep;
        }
        if (before == after)
            return;
        try
        {
            (after ? SessionLocked : SessionUnlocked)?.Invoke();
        }
        catch (Exception ex)
        {
            Program.Log($"Presence handler failed: {ex}");
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo plii);
}
