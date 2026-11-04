namespace Kazo.Bar.Infrastructure.Providers;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>
/// The app's <see cref="IWorkItemProvider"/> (US5.2): uses the work item source of the active
/// project's repository (the first one that has a source), Azure DevOps or GitHub Issues.
/// Without a source it returns nothing. Errors surface as <see cref="ProviderException"/>.
/// </summary>
public sealed class RepositoryWorkItemProvider : IWorkItemProvider
{
    private readonly ISettingsService _settings;
    private readonly IReadOnlyDictionary<WorkItemProviderKind, IWorkItemSourceClient> _clients;

    public RepositoryWorkItemProvider(ISettingsService settings, IEnumerable<IWorkItemSourceClient> clients)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clients = (clients ?? throw new ArgumentNullException(nameof(clients))).ToDictionary(c => c.Kind);
    }

    public string ProviderName => ActiveSource()?.Provider switch
    {
        WorkItemProviderKind.AzureDevOps => AzureDevOpsWorkItemsClient.ServiceName,
        WorkItemProviderKind.GitHub => "GitHub Issues",
        _ => "Not configured",
    };

    public Task<IReadOnlyList<WorkItem>> SearchAsync(string query)
    {
        var source = ActiveSource();
        return source == null
            ? Task.FromResult<IReadOnlyList<WorkItem>>([])
            : Client(source).SearchAsync(source, query);
    }

    public Task<WorkItem?> GetByIdAsync(string id)
    {
        var source = ActiveSource();
        return source == null ? Task.FromResult<WorkItem?>(null) : Client(source).GetAsync(source, id);
    }

    public string GetWebUrl(WorkItem item) => item?.Url ?? string.Empty;

    /// <summary>The work item source used for the active project, or null.</summary>
    public WorkItemSource? ActiveSource() =>
        _settings.GetActiveProject()?.GitRepositories.Select(r => r.WorkItems).FirstOrDefault(s => s != null);

    private IWorkItemSourceClient Client(WorkItemSource source) =>
        _clients.TryGetValue(source.Provider, out var client)
            ? client
            : throw new ProviderException($"{source.Provider} work items are not available in this host.");
}
