namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>Work item systems a repository can be linked to (US5.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkItemProviderKind>))]
public enum WorkItemProviderKind
{
    /// <summary>Azure DevOps Services (dev.azure.com).</summary>
    AzureDevOps,

    /// <summary>GitHub Issues of a repository.</summary>
    GitHub,
}

/// <summary>
/// Where a repository's work items come from (US5.2). The token is in the secret store
/// under <see cref="SecretRef"/>, never in the configuration (US2.4).
/// </summary>
public sealed record WorkItemSource
{
    [JsonPropertyName("provider")]
    public WorkItemProviderKind Provider { get; init; }

    /// <summary>Azure DevOps organization (dev.azure.com/<b>{organization}</b>).</summary>
    [JsonPropertyName("organization")]
    public string Organization { get; init; } = string.Empty;

    /// <summary>Azure DevOps project.</summary>
    [JsonPropertyName("project")]
    public string Project { get; init; } = string.Empty;

    /// <summary>GitHub repository as <c>owner/name</c>.</summary>
    [JsonPropertyName("repository")]
    public string Repository { get; init; } = string.Empty;

    [JsonPropertyName("secretRef")]
    public string SecretRef { get; init; } = string.Empty;
}

/// <summary>
/// GitHub follow-up of a repository (US7.1): workflow runs of GitHub Actions.
/// Agent sessions will be added when GitHub offers an API for them.
/// </summary>
public sealed record GitHubTracking
{
    /// <summary>GitHub repository as <c>owner/name</c>.</summary>
    [JsonPropertyName("repository")]
    public string Repository { get; init; } = string.Empty;

    [JsonPropertyName("trackActions")]
    public bool TrackActions { get; init; } = true;

    [JsonPropertyName("secretRef")]
    public string SecretRef { get; init; } = string.Empty;
}
