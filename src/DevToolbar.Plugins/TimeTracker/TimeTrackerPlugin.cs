namespace DevToolbar.Plugins.TimeTracker;

using DevToolbar.Core.Events;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Time tracking (US5.3) and work day (US5.5): Start/Stop on the active project and work item, the
/// timer freezes when the user is away and the absence is offered for labelling on return.
/// The panel itself is <see cref="TimeTrackerPanel"/>.
/// </summary>
public class TimeTrackerPlugin : IPlugin, IDisposable
{
    private readonly ITimeTrackingService _timeService;
    private readonly INotificationService _notificationService;
    private readonly EventAggregator _eventAggregator;

    public string UniqueId => "time-tracker";
    public string Name => "Time Tracker";
    public string Icon => "⏱";
    public bool IsEnabled { get; set; } = true;
    public event Action? OnStateChanged;

    public string CurrentProjectId { get; private set; } = string.Empty;
    public string? ActiveWorkItemId { get; private set; }
    public string? ActiveWorkItemTitle { get; private set; }

    public TimeTrackerPlugin(ITimeTrackingService timeService, INotificationService notificationService, EventAggregator eventAggregator)
    {
        _timeService = timeService;
        _notificationService = notificationService;
        _eventAggregator = eventAggregator;

        // Subscribe to work item changes from WorkItemsPlugin
        _eventAggregator.Subscribe<ActiveWorkItemChangedEvent>(OnWorkItemChanged);

        _timeService.OnIdleDetected += OnIdleDetected;
        _timeService.OnAbsenceEnded += OnAbsenceEnded;
    }

    private void OnIdleDetected()
    {
        if (_timeService.GetActive() == null && !WasRunningBeforeAway())
        {
            OnStateChanged?.Invoke();
            return;
        }
        _notificationService.ShowWarning("Timer paused while you are away.", "⏱ Away");
        OnStateChanged?.Invoke();
    }

    // The service closes the entry when the user leaves; the toast only matters for a running timer.
    private bool WasRunningBeforeAway() =>
        _timeService.Today?.Entries.LastOrDefault() is { EndTime: { } end } && end == _timeService.AwaySince;

    private void OnAbsenceEnded(IReadOnlyList<Absence> segments)
    {
        var start = segments.Min(a => a.Start);
        var end = segments.Max(a => a.End);
        _notificationService.ShowInfo(
            $"Welcome back: away {TimeFormat.Short(end - start)} ({start:HH:mm}–{end:HH:mm}). Open ⏱ to label it.",
            "⏱ Absence");
        OnStateChanged?.Invoke();
    }

    private void OnWorkItemChanged(ActiveWorkItemChangedEvent evt)
    {
        ActiveWorkItemId = evt.WorkItemId;
        ActiveWorkItemTitle = evt.WorkItemTitle;
        OnStateChanged?.Invoke();
    }

    public Task OnProjectChangedAsync(PluginContext context)
    {
        // The work item is NOT reset here: WorkItemsPlugin publishes the new project's active
        // item during the same project switch (possibly before this handler runs), so clearing
        // it would drop the link and time entries would be saved without a WorkItemId (US5.3).
        CurrentProjectId = context.Project.Id;
        return Task.CompletedTask;
    }

    /// <summary>Start or stop the timer on the current project and work item.</summary>
    public void ToggleTimer()
    {
        var active = _timeService.GetActive();
        if (active != null && active.ProjectId == CurrentProjectId)
            _timeService.Stop();
        else
            _timeService.Start(CurrentProjectId, ActiveWorkItemId);
        OnStateChanged?.Invoke();
    }

    /// <summary>Re-render the toolbar (after an absence was labelled).</summary>
    public void NotifyChanged() => OnStateChanged?.Invoke();

    public RenderFragment? Render() => builder =>
    {
        builder.OpenComponent<TimeTrackerPanel>(0);
        builder.AddAttribute(1, nameof(TimeTrackerPanel.Plugin), this);
        builder.CloseComponent();
    };

    public void Dispose()
    {
        _timeService.OnIdleDetected -= OnIdleDetected;
        _timeService.OnAbsenceEnded -= OnAbsenceEnded;
    }
}
