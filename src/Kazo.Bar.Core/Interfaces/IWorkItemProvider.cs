namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>
/// Abstracts work item providers (Azure DevOps, GitHub Issues, etc.)
/// </summary>
public interface IWorkItemProvider
{
    /// <summary>Provider name (e.g., "Azure DevOps", "GitHub").</summary>
    string ProviderName { get; }

    /// <summary>Search work items by query text.</summary>
    Task<IReadOnlyList<WorkItem>> SearchAsync(string query);

    /// <summary>Get a work item by ID.</summary>
    Task<WorkItem?> GetByIdAsync(string id);

    /// <summary>Get the web URL for a work item.</summary>
    string GetWebUrl(WorkItem item);
}
