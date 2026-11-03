namespace DevToolbar.Infrastructure.Providers;

using DevToolbar.Core.Models;

/// <summary>One work item system (Azure DevOps or GitHub Issues), queried for a configured source.</summary>
public interface IWorkItemSourceClient
{
    WorkItemProviderKind Kind { get; }

    /// <summary>Open items, most recently changed first (assigned to the token owner on Azure DevOps).</summary>
    Task<IReadOnlyList<WorkItem>> RecentAsync(WorkItemSource source, CancellationToken cancellationToken = default);

    /// <summary>Items whose title contains <paramref name="text"/>, or the item with that number.</summary>
    Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSource source, string text, CancellationToken cancellationToken = default);

    Task<WorkItem?> GetAsync(WorkItemSource source, string id, CancellationToken cancellationToken = default);
}
