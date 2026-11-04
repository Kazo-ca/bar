namespace Kazo.Bar.Tests.Unit.TimeTracking;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>Clock moved by hand, in UTC so local time equals the values written in the tests.</summary>
internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now;

    public ManualClock(DateTime start) => _now = new DateTimeOffset(start, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Set(DateTime at) => _now = new DateTimeOffset(at, TimeSpan.Zero);
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Presence driven by the test: last input time and lock state.</summary>
internal sealed class FakePresence : IUserPresenceService
{
    private readonly ManualClock _clock;

    public FakePresence(ManualClock clock)
    {
        _clock = clock;
        LastInput = clock.GetUtcNow().UtcDateTime;
    }

    public DateTime LastInput { get; set; }
    public bool IsLocked { get; private set; }
    public event Action? SessionLocked;
    public event Action? SessionUnlocked;

    public TimeSpan GetIdleTime() => _clock.GetUtcNow().UtcDateTime - LastInput;

    public void Input() => LastInput = _clock.GetUtcNow().UtcDateTime;

    public void Lock()
    {
        IsLocked = true;
        SessionLocked?.Invoke();
    }

    public void Unlock()
    {
        IsLocked = false;
        Input();
        SessionUnlocked?.Invoke();
    }
}

internal sealed class FakeGlobalSettings : IGlobalSettingsService
{
    public GlobalSettings Current { get; } = new();
    public event Action<GlobalSettings>? OnSettingsChanged;
    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;

    public Task UpdateAsync(Action<GlobalSettings> updateAction)
    {
        updateAction(Current);
        OnSettingsChanged?.Invoke(Current);
        return Task.CompletedTask;
    }
}

internal sealed class FakeProjects : ISettingsService
{
    public ProjectConfig? Active { get; set; } = new() { Id = "alpha", Name = "Alpha" };
    public event Action<ProjectConfig>? OnActiveProjectChanged;
    public Task<IReadOnlyList<ProjectConfig>> GetProjectsAsync() =>
        Task.FromResult<IReadOnlyList<ProjectConfig>>(Active == null ? [] : [Active]);
    public ProjectConfig? GetActiveProject() => Active;
    public Task SetActiveProjectAsync(string projectId)
    {
        OnActiveProjectChanged?.Invoke(Active!);
        return Task.CompletedTask;
    }
    public Task SaveProjectAsync(ProjectConfig project) => Task.CompletedTask;
}
