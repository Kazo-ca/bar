namespace DevToolbar.Infrastructure.Providers;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;

/// <summary>
/// Azure DevOps Services work items (US5.2): WIQL query then a batch read, with a PAT
/// (basic authentication, empty user name).
/// </summary>
public sealed class AzureDevOpsWorkItemsClient : IWorkItemSourceClient
{
    public const string ServiceName = "Azure DevOps";
    private const string ApiVersion = "7.1";
    private const int PageSize = 20;
    private const string Fields = "System.Id,System.Title,System.State,System.WorkItemType,System.AssignedTo";
    private const string ClosedStates = "'Closed', 'Done', 'Removed', 'Resolved'";

    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;

    public AzureDevOpsWorkItemsClient(HttpClient http, ISecretStore secrets)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public WorkItemProviderKind Kind => WorkItemProviderKind.AzureDevOps;

    public Task<IReadOnlyList<WorkItem>> RecentAsync(WorkItemSource source, CancellationToken cancellationToken = default) =>
        QueryAsync(source,
            "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.AssignedTo] = @Me " +
            $"AND [System.State] NOT IN ({ClosedStates}) ORDER BY [System.ChangedDate] DESC",
            cancellationToken);

    public async Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSource source, string text, CancellationToken cancellationToken = default)
    {
        var query = (text ?? string.Empty).Trim().TrimStart('#');
        if (query.Length == 0)
            return await RecentAsync(source, cancellationToken);
        if (int.TryParse(query, out _))
            return await GetAsync(source, query, cancellationToken) is { } item ? [item] : [];

        return await QueryAsync(source,
            "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project " +
            $"AND [System.Title] CONTAINS '{EscapeWiql(query)}' ORDER BY [System.ChangedDate] DESC",
            cancellationToken);
    }

    public async Task<WorkItem?> GetAsync(WorkItemSource source, string id, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(id.TrimStart('#'), out var number) || number <= 0)
            return null;
        try
        {
            return (await ReadAsync(source, [number], cancellationToken)).FirstOrDefault();
        }
        catch (ProviderException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    /// <summary>Web page of a work item.</summary>
    public static string WebUrl(WorkItemSource source, string id) =>
        $"https://dev.azure.com/{Uri.EscapeDataString(source.Organization)}/{Uri.EscapeDataString(source.Project)}/_workitems/edit/{Uri.EscapeDataString(id)}";

    /// <summary>WIQL string literal: single quotes are doubled.</summary>
    internal static string EscapeWiql(string text) => text.Replace("'", "''", StringComparison.Ordinal);

    private async Task<IReadOnlyList<WorkItem>> QueryAsync(WorkItemSource source, string wiql, CancellationToken cancellationToken)
    {
        var url = $"{ProjectBase(source)}/_apis/wit/wiql?api-version={ApiVersion}&$top={PageSize}";
        using var request = await CreateAsync(HttpMethod.Post, url, source);
        request.Content = JsonContent.Create(new { query = wiql });
        var result = await ProviderHttp.SendAsync<WiqlResult>(_http, request, ServiceName, cancellationToken);
        var ids = result.WorkItems.Select(w => w.Id).Take(PageSize).ToList();
        return ids.Count == 0 ? [] : await ReadAsync(source, ids, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkItem>> ReadAsync(WorkItemSource source, IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var idList = string.Join(',', ids.Select(i => i.ToString(CultureInfo.InvariantCulture)));
        var url = $"{OrganizationBase(source)}/_apis/wit/workitems?ids={idList}&fields={Fields}&errorPolicy=omit&api-version={ApiVersion}";
        using var request = await CreateAsync(HttpMethod.Get, url, source);
        var result = await ProviderHttp.SendAsync<BatchResult>(_http, request, ServiceName, cancellationToken);
        var byId = result.Value.Where(v => v != null).ToDictionary(v => v!.Id, v => v!);
        // Keep the WIQL order (most recently changed first).
        return ids.Where(byId.ContainsKey).Select(i => ToWorkItem(source, byId[i])).ToList();
    }

    private async Task<HttpRequestMessage> CreateAsync(HttpMethod method, string url, WorkItemSource source)
    {
        var token = await ProviderHttp.RequireTokenAsync(_secrets, source.SecretRef, ServiceName);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string OrganizationBase(WorkItemSource source) =>
        $"https://dev.azure.com/{Uri.EscapeDataString(source.Organization)}";

    private static string ProjectBase(WorkItemSource source) =>
        $"{OrganizationBase(source)}/{Uri.EscapeDataString(source.Project)}";

    private static WorkItem ToWorkItem(WorkItemSource source, BatchItem item)
    {
        string Field(string name) => item.Fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
        var assignedTo = item.Fields.TryGetValue("System.AssignedTo", out var identity) && identity.ValueKind == JsonValueKind.Object
            && identity.TryGetProperty("displayName", out var name) ? name.GetString() ?? string.Empty : string.Empty;
        var id = item.Id.ToString(CultureInfo.InvariantCulture);
        return new WorkItem
        {
            Id = id,
            Title = Field("System.Title"),
            State = Field("System.State"),
            Type = Field("System.WorkItemType"),
            AssignedTo = assignedTo,
            Url = WebUrl(source, id),
        };
    }

    private sealed record WiqlResult(List<WiqlReference> WorkItems);

    private sealed record WiqlReference(int Id);

    private sealed record BatchResult(List<BatchItem?> Value);

    private sealed record BatchItem(int Id, Dictionary<string, JsonElement> Fields);
}
