namespace Kazo.Bar.Core.Events;

using Kazo.Bar.Core.Models;

/// <summary>
/// Event published when the active project changes.
/// </summary>
public class ProjectChangedEvent
{
    public ProjectConfig Project { get; set; } = new();
}
