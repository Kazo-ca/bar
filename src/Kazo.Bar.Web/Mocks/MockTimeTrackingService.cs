namespace Kazo.Bar.Web.Mocks;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Core.TimeTracking;

/// <summary>
/// Mock time tracking service for web testing with idle detection support (US5.3) and a sample
/// work day (US5.5): arrival at 08:12 and an absence 11:45-13:00 waiting to be confirmed
/// (daily meeting, then lunch), cut by the real <see cref="AbsenceClassifier"/>.
/// </summary>
public class MockTimeTrackingService : ITimeTrackingService
{
    private readonly WorkDay _today = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
    public event Action<IReadOnlyList<Absence>>? OnAbsenceEnded { add { } remove { } }

    private TimeEntry? _activeEntry;
    private readonly List<TimeEntry> _entries = new();
    private DateTime _lastActivity = DateTime.Now;

    public event Action? OnTrackingChanged;
    public event Action? OnIdleDetected;

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(15);
    public bool IsIdlePaused { get; private set; }

    // Simulated idle detection timer
    private Timer? _idleTimer;

    public MockTimeTrackingService()
    {
        // Pre-populate with sample time entries for Time Report demo (US5.4)
        var today = DateTime.Today;
        _entries.AddRange(new[]
        {
            new TimeEntry { ProjectId = "proj-webapi", WorkItemId = "1234", StartTime = today.AddHours(9), EndTime = today.AddHours(10).AddMinutes(30), Description = "Fix login redirect" },
            new TimeEntry { ProjectId = "proj-webapi", WorkItemId = "1235", StartTime = today.AddHours(10).AddMinutes(45), EndTime = today.AddHours(12), Description = "Dark mode research" },
            new TimeEntry { ProjectId = "proj-webapi", StartTime = today.AddHours(13), EndTime = today.AddHours(14).AddMinutes(15), Description = "Code review" },
            new TimeEntry { ProjectId = "proj-frontend", WorkItemId = "1236", StartTime = today.AddHours(14).AddMinutes(30), EndTime = today.AddHours(16), Description = "Update docs" },
            new TimeEntry { ProjectId = "proj-webapi", WorkItemId = "1234", StartTime = today.AddDays(-1).AddHours(9), EndTime = today.AddDays(-1).AddHours(11), Description = "Login page testing" },
            new TimeEntry { ProjectId = "proj-frontend", WorkItemId = "1235", StartTime = today.AddDays(-1).AddHours(13), EndTime = today.AddDays(-1).AddHours(15).AddMinutes(30), Description = "UI components" },
            new TimeEntry { ProjectId = "proj-devops", StartTime = today.AddDays(-2).AddHours(10), EndTime = today.AddDays(-2).AddHours(12), Description = "Pipeline config" },
        });

        _today.FirstActivity = today.AddHours(8).AddMinutes(12);
        _today.LastLock = today.AddHours(11).AddMinutes(45);
        var rules = new TimeTrackingRules
        {
            DailyMeeting = new DailyMeetingRule { Enabled = true, Label = "Daily", Start = new(11, 45), End = new(12, 0), Days = Enum.GetValues<DayOfWeek>().ToList() },
        };
        _today.Absences.AddRange(AbsenceClassifier.Split(today.AddHours(11).AddMinutes(45), today.AddHours(13), rules, AbsenceCause.Locked, "proj-webapi", "1235"));
    }

    public DateTime? AwaySince => null;

    public WorkDay? Today => new()
    {
        Date = _today.Date,
        FirstActivity = _today.FirstActivity,
        LastLock = _today.LastLock,
        Entries = _entries.Where(e => e.StartTime.Date == DateTime.Today).ToList(),
        Absences = _today.Absences.ToList(),
    };

    public IReadOnlyList<Absence> PendingAbsences => _today.Absences.Where(a => a.PendingReview).ToList();

    public Task ClassifyAbsenceAsync(string absenceId, AbsenceKind kind, string? label = null)
    {
        var absence = _today.Absences.FirstOrDefault(a => a.Id == absenceId)
            ?? throw new KeyNotFoundException(absenceId);
        absence.Label = !string.IsNullOrWhiteSpace(label) ? label.Trim() : kind switch
        {
            AbsenceKind.Meeting => absence.Label ?? "Meeting",
            AbsenceKind.Worked => absence.Kind == AbsenceKind.Lunch ? "Worked through lunch" : "Worked away from the keyboard",
            _ => null,
        };
        absence.Kind = kind;
        absence.Automatic = false;
        absence.PendingReview = false;
        OnTrackingChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkDay>> GetWorkDaysAsync(DateOnly from, DateOnly to) =>
        Task.FromResult<IReadOnlyList<WorkDay>>(_today.Date >= from && _today.Date <= to ? [Today!] : []);

    public void ReportActivity()
    {
        _lastActivity = DateTime.Now;
        if (IsIdlePaused && _activeEntry != null)
        {
            // Resume tracking - start a new entry continuing where we left off
            IsIdlePaused = false;
            OnTrackingChanged?.Invoke();
        }
    }

    public TimeEntry Start(string projectId, string? workItemId = null)
    {
        // Stop current if running
        Stop();

        _activeEntry = new TimeEntry
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            StartTime = DateTime.Now
        };
        _entries.Add(_activeEntry);
        _lastActivity = DateTime.Now;
        IsIdlePaused = false;

        // Start idle detection timer (simulated for web testing)
        _idleTimer?.Dispose();
        _idleTimer = new Timer(_ =>
        {
            if (_activeEntry != null && !IsIdlePaused && (DateTime.Now - _lastActivity) >= IdleTimeout)
            {
                IsIdlePaused = true;
                OnIdleDetected?.Invoke();
                OnTrackingChanged?.Invoke();
            }
        }, null, IdleTimeout, TimeSpan.FromMinutes(1));

        OnTrackingChanged?.Invoke();
        return _activeEntry;
    }

    public TimeEntry? Stop()
    {
        if (_activeEntry == null) return null;

        _activeEntry.EndTime = DateTime.Now;
        var stopped = _activeEntry;
        _activeEntry = null;
        IsIdlePaused = false;
        _idleTimer?.Dispose();
        _idleTimer = null;
        OnTrackingChanged?.Invoke();
        return stopped;
    }

    public TimeEntry? GetActive() => _activeEntry;

    public Task<IReadOnlyList<TimeEntry>> GetEntriesAsync(DateTime from, string? projectId = null)
    {
        var counted = _today.Absences.Where(a => a.CountsAsWork).Select(a => new TimeEntry
        {
            Id = a.Id, ProjectId = a.ProjectId ?? "", WorkItemId = a.WorkItemId, StartTime = a.Start, EndTime = a.End,
            Description = a.Label ?? a.Kind.ToString(), AbsenceKind = a.Kind,
        });
        return Task.FromResult<IReadOnlyList<TimeEntry>>(
            _entries.Concat(counted)
                .Where(e => e.StartTime >= from && (projectId == null || e.ProjectId == projectId))
                .ToList().AsReadOnly());
    }

    public TimeSpan GetTodayTotal(string projectId)
    {
        var today = DateTime.Today;
        var counted = _today.Absences.Where(a => a.CountsAsWork && a.ProjectId == projectId)
            .Aggregate(TimeSpan.Zero, (total, a) => total + a.Duration);
        return counted + _entries
            .Where(e => e.ProjectId == projectId && e.StartTime.Date == today)
            .Aggregate(TimeSpan.Zero, (total, entry) => total + entry.Duration);
    }
}
