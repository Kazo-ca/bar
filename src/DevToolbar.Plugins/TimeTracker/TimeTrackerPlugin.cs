namespace DevToolbar.Plugins.TimeTracker;

using DevToolbar.Core.Events;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

/// <summary>
/// Plugin for tracking time spent on projects with idle detection (US5.3).
/// Associates time entries with the active work item (US5.3/US5.4).
/// </summary>
public class TimeTrackerPlugin : IPlugin
{
    private readonly ITimeTrackingService _timeService;
    private readonly INotificationService _notificationService;
    private readonly EventAggregator _eventAggregator;

    public string UniqueId => "time-tracker";
    public string Name => "Time Tracker";
    public string Icon => "⏱";
    public bool IsEnabled { get; set; } = true;
    public event Action? OnStateChanged;

    private string _currentProjectId = string.Empty;
    private string? _activeWorkItemId;
    private string? _activeWorkItemTitle;

    public TimeTrackerPlugin(ITimeTrackingService timeService, INotificationService notificationService, EventAggregator eventAggregator)
    {
        _timeService = timeService;
        _notificationService = notificationService;
        _eventAggregator = eventAggregator;

        // Subscribe to work item changes from WorkItemsPlugin
        _eventAggregator.Subscribe<ActiveWorkItemChangedEvent>(OnWorkItemChanged);

        // Subscribe to idle detection (US5.3 - notification de rappel)
        _timeService.OnIdleDetected += OnIdleDetected;
    }

    private void OnIdleDetected()
    {
        _notificationService.ShowWarning(
            $"Timer paused — no activity for {(int)_timeService.IdleTimeout.TotalMinutes} minutes.",
            "⏱ Idle Detected");
        OnStateChanged?.Invoke();
    }

    private void OnWorkItemChanged(ActiveWorkItemChangedEvent evt)
    {
        _activeWorkItemId = evt.WorkItemId;
        _activeWorkItemTitle = evt.WorkItemTitle;
        OnStateChanged?.Invoke();
    }

    public Task OnProjectChangedAsync(PluginContext context)
    {
        // The work item is NOT reset here: WorkItemsPlugin publishes the new project's active
        // item during the same project switch (possibly before this handler runs), so clearing
        // it would drop the link and time entries would be saved without a WorkItemId (US5.3).
        _currentProjectId = context.Project.Id;
        return Task.CompletedTask;
    }

    public RenderFragment? Render() => builder =>
    {
        var active = _timeService.GetActive();
        var isRunning = active != null && active.ProjectId == _currentProjectId;
        var todayTotal = _timeService.GetTodayTotal(_currentProjectId);
        var isIdle = _timeService.IsIdlePaused;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "plugin-timetracker");

        // Timer controls row
        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "timer-controls");

        // Start/Stop button
        builder.OpenElement(4, "button");
        builder.AddAttribute(5, "class", isRunning ? "timer-btn timer-btn-stop" : "timer-btn timer-btn-start");
        builder.AddAttribute(6, "onclick", EventCallback.Factory.Create(this, () =>
        {
            if (isRunning)
                _timeService.Stop();
            else
                _timeService.Start(_currentProjectId, _activeWorkItemId);
            OnStateChanged?.Invoke();
        }));
        builder.AddContent(7, isRunning ? "⏹ Stop" : "▶ Start");
        builder.CloseElement(); // button

        // Status indicator
        builder.OpenElement(8, "span");
        if (isIdle && isRunning)
        {
            builder.AddAttribute(9, "class", "timer-status idle");
            builder.AddContent(10, "💤 Idle");
        }
        else
        {
            builder.AddAttribute(9, "class", isRunning ? "timer-status running" : "timer-status stopped");
            builder.AddContent(10, isRunning ? "⏺ Recording" : "⏸ Stopped");
        }
        builder.CloseElement();

        builder.CloseElement(); // timer-controls

        // Active work item indicator
        builder.OpenElement(30, "div");
        builder.AddAttribute(31, "class", "timer-workitem");
        if (!string.IsNullOrEmpty(_activeWorkItemId))
        {
            builder.OpenElement(32, "span");
            builder.AddAttribute(33, "class", "timer-workitem-label");
            builder.AddContent(34, "📋");
            builder.CloseElement();
            builder.OpenElement(35, "span");
            builder.AddAttribute(36, "class", "timer-workitem-id");
            builder.AddContent(37, $"#{_activeWorkItemId}");
            builder.CloseElement();
            if (!string.IsNullOrEmpty(_activeWorkItemTitle))
            {
                builder.OpenElement(38, "span");
                builder.AddAttribute(39, "class", "timer-workitem-title");
                builder.AddContent(40, _activeWorkItemTitle);
                builder.CloseElement();
            }
        }
        else
        {
            builder.OpenElement(32, "span");
            builder.AddAttribute(33, "class", "timer-workitem-none");
            builder.AddContent(34, "No work item linked");
            builder.CloseElement();
        }
        builder.CloseElement(); // timer-workitem

        // Timer display
        builder.OpenElement(11, "div");
        builder.AddAttribute(12, "class", "timer-display");

        builder.OpenElement(13, "span");
        builder.AddAttribute(14, "class", "timer-duration");
        if (isRunning && active != null)
        {
            builder.AddContent(15, FormatDuration(active.Duration));
        }
        else
        {
            builder.AddContent(15, "00:00:00");
        }
        builder.CloseElement();

        builder.CloseElement(); // timer-display

        // Idle timeout info
        builder.OpenElement(25, "div");
        builder.AddAttribute(26, "class", "timer-idle-info");
        builder.OpenElement(27, "span");
        builder.AddAttribute(28, "class", "timer-idle-label");
        builder.AddContent(29, $"Idle timeout: {(int)_timeService.IdleTimeout.TotalMinutes} min");
        builder.CloseElement();
        builder.CloseElement(); // timer-idle-info

        // Today's total
        builder.OpenElement(16, "div");
        builder.AddAttribute(17, "class", "timer-today");

        builder.OpenElement(18, "span");
        builder.AddAttribute(19, "class", "timer-today-label");
        builder.AddContent(20, "Today:");
        builder.CloseElement();

        builder.OpenElement(21, "span");
        builder.AddAttribute(22, "class", "timer-today-value");
        builder.AddContent(23, FormatDuration(todayTotal));
        builder.CloseElement();

        builder.CloseElement(); // timer-today

        builder.CloseElement(); // plugin-timetracker
    };

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
}
