using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

namespace Kazo.Bar.Maui.Services;

/// <summary>
/// Placeholder for mock services used during MAUI development.
/// These will be replaced with real implementations as they are built.
/// </summary>

// Temporary: Re-use web mock services until real implementations are ready.
// In production, these would connect to real APIs (GitHub, Azure DevOps, etc.)
public class MockWorkItemProvider : IWorkItemProvider
{
    public string ProviderName => "Mock Provider";

    public Task<IReadOnlyList<WorkItem>> SearchAsync(string query) =>
        Task.FromResult<IReadOnlyList<WorkItem>>(Array.Empty<WorkItem>());

    public Task<WorkItem?> GetByIdAsync(string id) =>
        Task.FromResult<WorkItem?>(null);

    public string GetWebUrl(WorkItem item) => "#";
}

public class MockTimeTrackingService : ITimeTrackingService
{
    private TimeEntry? _activeEntry;
    private readonly List<TimeEntry> _entries = new();

    public event Action? OnTrackingChanged;
    public event Action? OnIdleDetected;

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(15);
    public bool IsIdlePaused => false;

    public void ReportActivity() { /* no-op in mock */ }

    public TimeEntry Start(string projectId, string? workItemId = null)
    {
        Stop();
        _activeEntry = new TimeEntry
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            StartTime = DateTime.Now
        };
        _entries.Add(_activeEntry);
        OnTrackingChanged?.Invoke();
        return _activeEntry;
    }

    public TimeEntry? Stop()
    {
        if (_activeEntry == null) return null;
        _activeEntry.EndTime = DateTime.Now;
        var stopped = _activeEntry;
        _activeEntry = null;
        OnTrackingChanged?.Invoke();
        return stopped;
    }

    public TimeEntry? GetActive() => _activeEntry;

    public Task<IReadOnlyList<TimeEntry>> GetEntriesAsync(DateTime from, string? projectId = null) =>
        Task.FromResult<IReadOnlyList<TimeEntry>>(
            _entries.Where(e => e.StartTime >= from && (projectId == null || e.ProjectId == projectId)).ToList().AsReadOnly());

    public TimeSpan GetTodayTotal(string projectId)
    {
        var today = DateTime.Today;
        return _entries
            .Where(e => e.ProjectId == projectId && e.StartTime.Date == today)
            .Aggregate(TimeSpan.Zero, (total, entry) => total + entry.Duration);
    }
}

public class MockCiCdService : ICiCdService
{
    private readonly List<CiCdSession> _sessions = new();
    public event Action? OnSessionsUpdated;

    public TimeSpan PollingInterval => TimeSpan.FromSeconds(30);

    public Task StartPollingAsync(string projectId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<CiCdSession>> GetSessionsAsync(string projectId) =>
        Task.FromResult<IReadOnlyList<CiCdSession>>(_sessions.AsReadOnly());

    public int GetUnreadCount(string projectId) =>
        _sessions.Count(s => !s.IsRead && s.Status == CiCdStatus.Completed);

    public void MarkAsRead(string sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null) { session.IsRead = true; OnSessionsUpdated?.Invoke(); }
    }

    public void MarkAllAsRead(string projectId)
    {
        foreach (var s in _sessions) s.IsRead = true;
        OnSessionsUpdated?.Invoke();
    }
}
