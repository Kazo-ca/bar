namespace DevToolbar.Core.Interfaces;

/// <summary>
/// Tells the time tracker whether someone is at the keyboard (US5.3): Windows session lock and
/// unlock, and how long since the last keyboard or mouse input (any application).
/// </summary>
public interface IUserPresenceService
{
    /// <summary>Time since the last keyboard or mouse input anywhere in the session.</summary>
    TimeSpan GetIdleTime();

    /// <summary>The Windows session is locked (or the machine is asleep).</summary>
    bool IsLocked { get; }

    /// <summary>The session was locked, or the machine went to sleep. May fire on any thread.</summary>
    event Action? SessionLocked;

    /// <summary>The session was unlocked, or the machine woke up. May fire on any thread.</summary>
    event Action? SessionUnlocked;
}
