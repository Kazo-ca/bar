namespace Kazo.Bar.Core.Events;

/// <summary>
/// Event published when the active work item changes (US5.2/US5.3 integration).
/// </summary>
public class ActiveWorkItemChangedEvent
{
    public string? WorkItemId { get; set; }
    public string? WorkItemTitle { get; set; }
}
