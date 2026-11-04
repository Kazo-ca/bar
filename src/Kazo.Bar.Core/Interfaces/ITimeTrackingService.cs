namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>
/// Time tracking (US5.3) and the work day (US5.5): timer entries per project and work item,
/// absences detected from inactivity or session lock, first activity and last lock of the day.
/// </summary>
public interface ITimeTrackingService
{
    /// <summary>Start tracking time for a project.</summary>
    TimeEntry Start(string projectId, string? workItemId = null);

    /// <summary>Stop the current active entry.</summary>
    TimeEntry? Stop();

    /// <summary>Get the currently active time entry, if any. Null while the timer is frozen by an absence.</summary>
    TimeEntry? GetActive();

    /// <summary>
    /// Entries that started at or after <paramref name="from"/>, for one project or all of them.
    /// Counted absences (meeting, worked, other) are included with <see cref="TimeEntry.AbsenceKind"/> set.
    /// </summary>
    Task<IReadOnlyList<TimeEntry>> GetEntriesAsync(DateTime from, string? projectId = null);

    /// <summary>Get total tracked time for a project today, counted absences included.</summary>
    TimeSpan GetTodayTotal(string projectId);

    /// <summary>Event fired when tracking state changes.</summary>
    event Action? OnTrackingChanged;

    /// <summary>Inactivity delay after which the timer freezes (global rule, or the repository's).</summary>
    TimeSpan IdleTimeout { get; }

    /// <summary>Report user activity in the toolbar (resets the idle timer, ends an idle absence).</summary>
    void ReportActivity();

    /// <summary>The user is away (idle or locked) and the timer is frozen.</summary>
    bool IsIdlePaused { get; }

    /// <summary>Event fired when the user goes away (idle or session lock).</summary>
    event Action? OnIdleDetected;

    /// <summary>Since when the user is away; null when present (US5.3).</summary>
    DateTime? AwaySince => null;

    /// <summary>The work day being recorded (first activity, last lock, entries, absences) (US5.5).</summary>
    WorkDay? Today => null;

    /// <summary>Absence segments of today not yet confirmed or labelled by the user.</summary>
    IReadOnlyList<Absence> PendingAbsences => [];

    /// <summary>Label an absence segment (meeting, lunch, worked, other with a text, removed).</summary>
    Task ClassifyAbsenceAsync(string absenceId, AbsenceKind kind, string? label = null) => Task.CompletedTask;

    /// <summary>Recorded work days between two dates (inclusive), for reports (US5.4).</summary>
    Task<IReadOnlyList<WorkDay>> GetWorkDaysAsync(DateOnly from, DateOnly to) =>
        Task.FromResult<IReadOnlyList<WorkDay>>([]);

    /// <summary>The user is back: the segments of the absence that just ended (US5.3).</summary>
    event Action<IReadOnlyList<Absence>>? OnAbsenceEnded { add { } remove { } }
}
