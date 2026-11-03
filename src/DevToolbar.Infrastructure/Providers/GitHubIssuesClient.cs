namespace DevToolbar.Infrastructure.Providers;

using System.Text.Json.Serialization;
using DevToolbar.Core.Models;

/// <summary>GitHub Issues of a repository (US5.2), through the REST API with a token (PAT for now).</summary>
public sealed class GitHubIssuesClient : IWorkItemSourceClient
{
    private const int PageSize = 20;
    private readonly GitHubApi _api;

    public GitHubIssuesClient(GitHubApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public WorkItemProviderKind Kind => WorkItemProviderKind.GitHub;

    public async Task<IReadOnlyList<WorkItem>> RecentAsync(WorkItemSource source, CancellationToken cancellationToken = default)
    {
        var url = $"{GitHubApi.RepositoryPath(source.Repository)}/issues?state=open&sort=updated&per_page={PageSize}";
        var issues = await _api.GetAsync<List<Issue>>(url, source.SecretRef, cancellationToken);
        return issues.Where(i => i.PullRequest == null).Select(ToWorkItem).ToList();
    }

    public async Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSource source, string text, CancellationToken cancellationToken = default)
    {
        var query = (text ?? string.Empty).Trim().TrimStart('#');
        if (query.Length == 0)
            return await RecentAsync(source, cancellationToken);
        if (int.TryParse(query, out _))
            return await GetAsync(source, query, cancellationToken) is { } item ? [item] : [];

        var q = Uri.EscapeDataString($"{query} in:title repo:{source.Repository} is:issue");
        var result = await _api.GetAsync<SearchResult>($"search/issues?q={q}&per_page={PageSize}", source.SecretRef, cancellationToken);
        return result.Items.Select(ToWorkItem).ToList();
    }

    public async Task<WorkItem?> GetAsync(WorkItemSource source, string id, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(id.TrimStart('#'), out var number) || number <= 0)
            return null;
        try
        {
            var issue = await _api.GetAsync<Issue>($"{GitHubApi.RepositoryPath(source.Repository)}/issues/{number}", source.SecretRef, cancellationToken);
            return issue.PullRequest == null ? ToWorkItem(issue) : null;
        }
        catch (ProviderException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    private static WorkItem ToWorkItem(Issue issue) => new()
    {
        Id = issue.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Title = issue.Title,
        State = issue.State == "open" ? "Open" : "Closed",
        Type = "Issue",
        AssignedTo = issue.Assignee?.Login ?? string.Empty,
        Url = issue.HtmlUrl,
    };

    private sealed record Issue(
        int Number,
        string Title,
        string State,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        User? Assignee,
        [property: JsonPropertyName("pull_request")] object? PullRequest);

    private sealed record User(string Login);

    private sealed record SearchResult(List<Issue> Items);
}
