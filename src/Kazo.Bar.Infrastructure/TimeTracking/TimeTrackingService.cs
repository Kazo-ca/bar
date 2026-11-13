namespace Kazo.Bar.Infrastructure.TimeTracking;

using System.Text.Json;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Core.TimeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real time tracking (US5.3) and work day (US5.5).
/// <list type="bullet">
/// <item>The timer freezes when the session is locked or after the idle delay (global rule, or the
/// active project's repository rule). On return the absence is cut by <see cref="AbsenceClassifier"/>
/// (daily meeting, lunch, rest to label) and the timer resumes on the same project and work item.</item>
/// <item>The first activity and the last lock of each day are recorded.</item>
/// <item>Each day is one value in <c>data\time-yyyy-MM.json</c> (key <c>yyyy-MM-dd</c>) through
/// <see cref="IStorageService"/>; <c>data\time-tracking.json</c> keeps a heartbeat so a crash or a
/// restart closes the open entry at the last moment the application was alive.</item>
/// </list>
/// </summary>
public sealed class TimeTrackingService : ITimeTrackingService, IDisposable
{
    public const string StateNamespace = "time-tracking";
    public const string StateKey = "state";
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    /// <summary>Shorter absences (a few seconds of lock) are not shown to the user.</summary>
    public static readonly TimeSpan MinimumAbsence = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);
    private const string DayKeyFormat = "yyyy-MM-dd";

    private readonly IStorageService _storage;
    private readonly IUserPresenceService _presence;
    private readonly IGlobalSettingsService _globalSettings;
    private readonly ISettingsService _settings;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private WorkDay _today;
    private TimeEntry? _active;
    private DateTime? _awaySince;
    private AbsenceCause _awayCause;
    private (string ProjectId, string? WorkItemId)? _resume;
    private DateTime _lastActivity;
    private DateTime _lastHeartbeatSaved;
    private ITimer? _timer;

    public TimeTrackingService(
        IStorageService storage,
        IUserPresenceService presence,
        IGlobalSettingsService globalSettings,
        ISettingsService settings,
        TimeProvider? time = null,
        ILogger<TimeTrackingService>? logger = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _globalSettings = globalSettings ?? throw new ArgumentNullException(nameof(globalSettings));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<TimeTrackingService>.Instance;
        _today = new WorkDay { Date = DateOnly.FromDateTime(Now) };
        _lastActivity = Now;
    }

    public event Action? OnTrackingChanged;
    public event Action? OnIdleDetected;
    public event Action<IReadOnlyList<Absence>>? OnAbsenceEnded;

    private DateTime Now => _time.GetLocalNow().DateTime;

    /// <summary>Rules of the active project's repository, or the global ones.</summary>
    public TimeTrackingRules Rules
    {
        get
        {
            var global = _globalSettings.Current.TimeTracking ?? new TimeTrackingRules();
            var overrides = _settings.GetActiveProject()?.GitRepositories
                .Select(r => r.TimeTracking)
                .FirstOrDefault(t => t != null);
            return global.With(overrides);
        }
    }

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(Math.Max(1, Rules.IdleMinutes));

    public bool IsIdlePaused { get { lock (_gate) return _awaySince != null; } }

    public DateTime? AwaySince { get { lock (_gate) return _awaySince; } }

    public WorkDay? Today { get { lock (_gate) return Snapshot(_today); } }

    public IReadOnlyList<Absence> PendingAbsences
    {
        get { lock (_gate) return _today.Absences.Where(a => a.PendingReview).Select(Copy).ToList(); }
    }

    public TimeEntry? GetActive() { lock (_gate) return _active == null ? null : Copy(_active); }

    /// <summary>
    /// Loads today, closes an entry left open by a crash or restart, then starts watching
    /// presence. Call once at startup.
    /// </summary>
    public async Task InitializeAsync(bool startPolling = true)
    {
        var now = Now;
        var state = await SafeGetAsync<TrackerState>(StateNamespace, StateKey);
        var today = await LoadDayAsync(DateOnly.FromDateTime(now)) ?? new WorkDay { Date = DateOnly.FromDateTime(now) };
        TimeEntry? resumed = null;
        WorkDay? previousDay = null;

        if (state?.LastHeartbeat is { } heartbeat && state.OpenEntryDate is { } openDate)
        {
            var day = openDate == today.Date ? today : await LoadDayAsync(openDate);
            var open = day?.Entries.LastOrDefault(e => e.EndTime == null);
            if (day != null && open != null)
            {
                open.EndTime = heartbeat < open.StartTime ? open.StartTime : heartbeat;
                day.LastLock = Max(day.LastLock, open.EndTime);
                if (day == today)
                {
                    // Same day: the time the application was not running is an absence to label,
                    // and the timer carries on as if it had been frozen.
                    if (now - heartbeat >= MinimumAbsence)
                        today.Absences.AddRange(AbsenceClassifier.Split(heartbeat, now, Rules, AbsenceCause.AppClosed, open.ProjectId, open.WorkItemId));
                    resumed = NewEntry(open.ProjectId, open.WorkItemId, now);
                    today.Entries.Add(resumed);
                }
                else
                {
                    previousDay = day;
                }
                _logger.LogInformation("Closed the timer entry left open at {Heartbeat}.", heartbeat);
            }
        }

        // Never leave an entry open other than the resumed one.
        foreach (var e in today.Entries.Where(e => e.EndTime == null && e != resumed))
            e.EndTime = e.StartTime;

        lock (_gate)
        {
            _today = today;
            _today.FirstActivity ??= now;
            _active = resumed;
            _lastActivity = now;
        }

        if (previousDay != null)
            await SaveDayAsync(previousDay);
        await PersistAsync();

        _presence.SessionLocked += OnSessionLocked;
        _presence.SessionUnlocked += OnSessionUnlocked;
        if (startPolling)
            _timer = _time.CreateTimer(_ => SafePoll(), null, PollInterval, PollInterval);
        OnTrackingChanged?.Invoke();
    }

    public TimeEntry Start(string projectId, string? workItemId = null)
    {
        TimeEntry started;
        lock (_gate)
        {
            var now = Now;
            RollOverIfNeeded(now);
            CloseActive(now);
            _resume = null;
            started = NewEntry(projectId, workItemId, now);
            _today.Entries.Add(started);
            _today.FirstActivity ??= now;
            _active = started;
            _lastActivity = now;
            // Starting by hand means the user is here.
            _awaySince = null;
        }
        _ = PersistAsync();
        OnTrackingChanged?.Invoke();
        return Copy(started);
    }

    public TimeEntry? Stop()
    {
        TimeEntry? stopped;
        bool wasResuming;
        lock (_gate)
        {
            stopped = CloseActive(Now);
            // Stopping while away: do not restart on return.
            wasResuming = _resume != null;
            _resume = null;
        }
        if (stopped == null && !wasResuming)
            return null;
        _ = PersistAsync();
        OnTrackingChanged?.Invoke();
        return stopped == null ? null : Copy(stopped);
    }

    public void ReportActivity()
    {
        IReadOnlyList<Absence>? ended = null;
        lock (_gate)
        {
            var now = Now;
            _lastActivity = now;
            if (_awaySince != null && _awayCause == AbsenceCause.Idle)
                ended = EndAway(now);
        }
        if (ended != null)
            AfterReturn(ended);
    }

    public TimeSpan GetTodayTotal(string projectId)
    {
        lock (_gate)
        {
            var now = Now;
            var timer = _today.Entries
                .Where(e => e.ProjectId == projectId)
                .Aggregate(TimeSpan.Zero, (sum, e) => sum + ((e.EndTime ?? now) - e.StartTime));
            var counted = _today.Absences
                .Where(a => a.CountsAsWork && a.ProjectId == projectId)
                .Aggregate(TimeSpan.Zero, (sum, a) => sum + a.Duration);
            return timer + counted;
        }
    }

    public async Task<IReadOnlyList<TimeEntry>> GetEntriesAsync(DateTime from, string? projectId = null)
    {
        var days = await GetWorkDaysAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(Now));
        return days
            .SelectMany(d => d.Entries.Concat(d.Absences.Where(a => a.CountsAsWork).Select(AsEntry)))
            .Where(e => e.StartTime >= from && (projectId == null || e.ProjectId == projectId))
            .OrderBy(e => e.StartTime)
            .ToList();
    }

    public async Task<IReadOnlyList<WorkDay>> GetWorkDaysAsync(DateOnly from, DateOnly to)
    {
        // Let the writes already queued (a day that just ended) land before reading the files.
        await _writeLock.WaitAsync();
        _writeLock.Release();

        var result = new List<WorkDay>();
        WorkDay today;
        lock (_gate) today = Snapshot(_today);

        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            IReadOnlyList<string> keys;
            try
            {
                keys = await _storage.GetKeysAsync(MonthNamespace(month));
            }
            catch (Exception ex) when (IsStorageFailure(ex))
            {
                _logger.LogError(ex, "Could not read time entries for {Month}.", month.ToString("yyyy-MM"));
                continue;
            }
            foreach (var key in keys.Order(StringComparer.Ordinal))
            {
                if (!DateOnly.TryParseExact(key, DayKeyFormat, out var date) || date < from || date > to || date == today.Date)
                    continue;
                if (await SafeGetAsync<WorkDay>(MonthNamespace(date), key) is { } day)
                    result.Add(day);
            }
        }
        if (today.Date >= from && today.Date <= to)
            result.Add(today);
        return result.OrderBy(d => d.Date).ToList();
    }

    public async Task ClassifyAbsenceAsync(string absenceId, AbsenceKind kind, string? label = null)
    {
        lock (_gate)
        {
            var absence = _today.Absences.FirstOrDefault(a => a.Id == absenceId)
                ?? throw new KeyNotFoundException($"No absence {absenceId} today.");
            Classify(absence, kind, label);
        }
        await PersistAsync();
        OnTrackingChanged?.Invoke();
    }

    public Task ClassifyAbsenceAsync(DateOnly date, string absenceId, AbsenceKind kind, string? label = null) =>
        UpdateDayAsync(date, createIfMissing: false, day =>
        {
            var absence = day.Absences.FirstOrDefault(a => a.Id == absenceId)
                ?? throw new KeyNotFoundException($"No absence {absenceId} on {date:yyyy-MM-dd}.");
            Classify(absence, kind, label);
        });

    public Task SetDayOffAsync(DateOnly date, DayOff? dayOff)
    {
        var value = dayOff?.Normalized();
        return UpdateDayAsync(date, createIfMissing: true, day => day.DayOff = value?.Copy());
    }

    /// <summary>
    /// Changes today in memory, or reads, changes and writes back a past day inside the write lock
    /// (US5.9). Unlike the background writes, a failure here is thrown so the report can say so.
    /// </summary>
    private async Task UpdateDayAsync(DateOnly date, bool createIfMissing, Action<WorkDay> change)
    {
        bool isToday;
        lock (_gate)
        {
            if (date > _today.Date)
                throw new ArgumentOutOfRangeException(nameof(date), date, "Only past days and today can be changed.");
            isToday = date == _today.Date;
            if (isToday)
                change(_today);
        }

        if (!isToday)
        {
            await _writeLock.WaitAsync();
            try
            {
                var key = date.ToString(DayKeyFormat);
                var day = await _storage.GetAsync<WorkDay>(MonthNamespace(date), key);
                if (day == null)
                {
                    if (!createIfMissing)
                        throw new KeyNotFoundException($"Nothing recorded on {key}.");
                    day = new WorkDay { Date = date };
                }
                change(day);
                await _storage.SetAsync(MonthNamespace(date), key, day);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        else
        {
            await PersistAsync();
        }
        OnTrackingChanged?.Invoke();
    }

    private static void Classify(Absence absence, AbsenceKind kind, string? label)
    {
        absence.Label = string.IsNullOrWhiteSpace(label) ? DefaultLabel(kind, absence) : label.Trim();
        absence.Kind = kind;
        absence.Automatic = false;
        absence.PendingReview = false;
    }

    /// <summary>Closes the running entry and records the end of the day (application exit).</summary>
    public async Task ShutdownAsync()
    {
        _timer?.Dispose();
        _timer = null;
        lock (_gate)
        {
            var now = Now;
            var awaySince = _awaySince;
            CloseActive(now);
            _today.LastLock = awaySince ?? now;
        }
        await PersistAsync(closing: true);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _presence.SessionLocked -= OnSessionLocked;
        _presence.SessionUnlocked -= OnSessionUnlocked;
    }

    // ── Presence ──────────────────────────────────────────────────────────

    /// <summary>One presence check (every <see cref="PollInterval"/>). Internal for tests.</summary>
    internal void Poll()
    {
        IReadOnlyList<Absence>? ended = null;
        var wentAway = false;
        bool save;
        var timeout = IdleTimeout;
        var idle = _presence.GetIdleTime();
        var locked = _presence.IsLocked;
        lock (_gate)
        {
            var now = Now;
            save = RollOverIfNeeded(now);
            var lastInput = now - idle;
            if (lastInput > _lastActivity)
                _lastActivity = lastInput;

            if (_awaySince == null)
            {
                if (!locked && now - _lastActivity >= timeout)
                {
                    BeginAway(_lastActivity, AbsenceCause.Idle);
                    wentAway = true;
                }
                else if (idle < timeout && _today.FirstActivity == null)
                {
                    _today.FirstActivity = lastInput;
                    save = true;
                }
            }
            else if (_awayCause == AbsenceCause.Idle && _lastActivity > _awaySince)
            {
                ended = EndAway(_lastActivity);
            }

            if (now - _lastHeartbeatSaved >= HeartbeatInterval)
                save = true;
        }

        if (wentAway)
            AfterAway();
        else if (ended != null)
            AfterReturn(ended);
        else if (save)
            _ = PersistAsync();
    }

    private void SafePoll()
    {
        try
        {
            Poll();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Time tracking presence check failed.");
        }
    }

    private void OnSessionLocked()
    {
        var pause = Rules.PauseOnLock;
        lock (_gate)
        {
            var now = Now;
            RollOverIfNeeded(now);
            _today.LastLock = now;
            if (pause)
            {
                if (_awaySince == null)
                    BeginAway(now, AbsenceCause.Locked);
                else
                    _awayCause = AbsenceCause.Locked; // idle, then locked: only the unlock ends it
            }
        }
        if (pause)
            AfterAway();
        else
            _ = PersistAsync();
    }

    private void OnSessionUnlocked()
    {
        IReadOnlyList<Absence>? ended = null;
        lock (_gate)
        {
            var now = Now;
            _lastActivity = now;
            if (_awaySince != null)
                ended = EndAway(now);
            RollOverIfNeeded(now);
            _today.FirstActivity ??= now;
        }
        if (ended != null)
            AfterReturn(ended);
        else
            _ = PersistAsync();
    }

    /// <summary>Freezes the timer at <paramref name="at"/>. Caller holds the lock.</summary>
    private void BeginAway(DateTime at, AbsenceCause cause)
    {
        _awaySince = at;
        _awayCause = cause;
        if (_active != null)
        {
            _resume = (_active.ProjectId, _active.WorkItemId);
            CloseActive(at);
        }
    }

    /// <summary>Ends the absence at <paramref name="at"/> and resumes the timer. Caller holds the lock.</summary>
    private IReadOnlyList<Absence> EndAway(DateTime at)
    {
        var since = _awaySince!.Value;
        _awaySince = null;
        IReadOnlyList<Absence> segments = [];

        if (DateOnly.FromDateTime(since) != DateOnly.FromDateTime(at))
        {
            // Away overnight: the end of one day and the start of the next, not an absence.
            RollOverIfNeeded(at);
            _today.FirstActivity ??= at;
        }
        else if (at - since >= MinimumAbsence)
        {
            segments = AbsenceClassifier.Split(since, at, Rules, _awayCause, _resume?.ProjectId, _resume?.WorkItemId);
            _today.Absences.AddRange(segments);
        }

        if (_resume is { } resume)
        {
            _active = NewEntry(resume.ProjectId, resume.WorkItemId, at);
            _today.Entries.Add(_active);
            _resume = null;
        }
        return segments.Select(Copy).ToList();
    }

    private void AfterAway()
    {
        _ = PersistAsync();
        OnIdleDetected?.Invoke();
        OnTrackingChanged?.Invoke();
    }

    private void AfterReturn(IReadOnlyList<Absence> segments)
    {
        _ = PersistAsync();
        if (segments.Count > 0)
            OnAbsenceEnded?.Invoke(segments);
        OnTrackingChanged?.Invoke();
    }

    // ── Day bookkeeping (caller holds the lock) ───────────────────────────

    /// <summary>Moves to a new day at midnight; a running entry is cut in two. Returns true when the day changed.</summary>
    private bool RollOverIfNeeded(DateTime now)
    {
        var date = DateOnly.FromDateTime(now);
        if (date == _today.Date)
            return false;

        var previous = _today;
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        var running = _active;
        if (running != null)
        {
            running.EndTime = midnight;
            previous.LastLock = Max(previous.LastLock, midnight);
        }
        // Left without locking (idle until the end of the day): the day ended at the last input.
        if (_awaySince is { } since && DateOnly.FromDateTime(since) == previous.Date)
            previous.LastLock = Max(previous.LastLock, since);
        _ = SaveDayAsync(Snapshot(previous));

        // A day is written each time it changes, so the new day starts empty.
        _today = new WorkDay { Date = date };
        _active = null;
        if (running != null)
        {
            _active = NewEntry(running.ProjectId, running.WorkItemId, midnight);
            _today.Entries.Add(_active);
        }
        return true;
    }

    private TimeEntry? CloseActive(DateTime at)
    {
        if (_active == null)
            return null;
        _active.EndTime = at < _active.StartTime ? _active.StartTime : at;
        var closed = _active;
        _active = null;
        return closed;
    }

    private static TimeEntry NewEntry(string projectId, string? workItemId, DateTime start) =>
        new() { ProjectId = projectId, WorkItemId = workItemId, StartTime = start };

    // ── Persistence ───────────────────────────────────────────────────────

    private static string MonthNamespace(DateOnly date) => $"time-{date:yyyy-MM}";

    /// <summary>Writes today and the heartbeat. Exposed for tests that wait for the writes.</summary>
    internal async Task PersistAsync(bool closing = false)
    {
        // The snapshot is taken inside the write lock, so writes land in the order of the changes.
        await _writeLock.WaitAsync();
        try
        {
            WorkDay day;
            TrackerState state;
            lock (_gate)
            {
                var now = Now;
                day = Snapshot(_today);
                state = new TrackerState
                {
                    LastHeartbeat = now,
                    OpenEntryDate = !closing && (_active != null || _resume != null) ? _today.Date : null,
                };
                _lastHeartbeatSaved = now;
            }
            await TryWriteAsync(MonthNamespace(day.Date), day.Date.ToString(DayKeyFormat), day);
            await TryWriteAsync(StateNamespace, StateKey, state);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task SaveDayAsync(WorkDay day)
    {
        await _writeLock.WaitAsync();
        try
        {
            await TryWriteAsync(MonthNamespace(day.Date), day.Date.ToString(DayKeyFormat), day);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private Task<WorkDay?> LoadDayAsync(DateOnly date) =>
        SafeGetAsync<WorkDay>(MonthNamespace(date), date.ToString(DayKeyFormat));

    /// <summary>Writes one value; caller holds the write lock. Failures are logged, never thrown.</summary>
    private async Task TryWriteAsync<T>(string ns, string key, T value)
    {
        try
        {
            await _storage.SetAsync(ns, key, value);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogError(ex, "Could not save time tracking data {Namespace}/{Key}.", ns, key);
        }
    }

    private async Task<T?> SafeGetAsync<T>(string ns, string key) where T : class
    {
        try
        {
            return await _storage.GetAsync<T>(ns, key);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            // A file from a newer version throws: it is left untouched and the day starts empty.
            _logger.LogError(ex, "Could not read time tracking data {Namespace}/{Key}.", ns, key);
            return null;
        }
    }

    private static bool IsStorageFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or NotSupportedException;

    // ── Copies (callers never get the live objects) ───────────────────────

    private static WorkDay Snapshot(WorkDay day) => new()
    {
        Date = day.Date,
        FirstActivity = day.FirstActivity,
        LastLock = day.LastLock,
        Entries = day.Entries.Select(Copy).ToList(),
        Absences = day.Absences.Select(Copy).ToList(),
        DayOff = day.DayOff?.Copy(),
    };

    private static TimeEntry Copy(TimeEntry e) => new()
    {
        Id = e.Id,
        ProjectId = e.ProjectId,
        WorkItemId = e.WorkItemId,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        Description = e.Description,
        AbsenceKind = e.AbsenceKind,
    };

    private static Absence Copy(Absence a) => new()
    {
        Id = a.Id,
        GroupId = a.GroupId,
        Start = a.Start,
        End = a.End,
        Kind = a.Kind,
        Label = a.Label,
        Cause = a.Cause,
        Automatic = a.Automatic,
        PendingReview = a.PendingReview,
        ProjectId = a.ProjectId,
        WorkItemId = a.WorkItemId,
    };

    private static TimeEntry AsEntry(Absence a) => new()
    {
        Id = a.Id,
        ProjectId = a.ProjectId ?? string.Empty,
        WorkItemId = a.WorkItemId,
        StartTime = a.Start,
        EndTime = a.End,
        Description = a.Label ?? a.Kind.ToString(),
        AbsenceKind = a.Kind,
    };

    private static string? DefaultLabel(AbsenceKind kind, Absence current) => kind switch
    {
        AbsenceKind.Meeting => current.Kind == AbsenceKind.Meeting ? current.Label ?? "Meeting" : "Meeting",
        AbsenceKind.Worked => current.Kind == AbsenceKind.Lunch ? "Worked through lunch" : "Worked away from the keyboard",
        _ => null,
    };

    private static DateTime? Max(DateTime? a, DateTime? b) => a == null ? b : b == null ? a : (a > b ? a : b);

    /// <summary>Survives a crash: when the application was last alive and whether a timer was running.</summary>
    internal sealed class TrackerState
    {
        public DateTime? LastHeartbeat { get; set; }
        public DateOnly? OpenEntryDate { get; set; }
    }
}
