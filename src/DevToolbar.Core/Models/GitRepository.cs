namespace DevToolbar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// A Git repository tracked by the Git plugin for a project.
/// A project can hold several repositories (e.g. API + shared library).
/// </summary>
public sealed record GitRepository
{
    /// <summary>Display name shown in the toolbar repository selector.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Local working-copy folder (where git commands run).</summary>
    [JsonPropertyName("localPath")]
    public string Path { get; init; } = string.Empty;

    /// <summary>Optional remote URL (https or ssh).</summary>
    [JsonPropertyName("remoteUrl")]
    public string RemoteUrl { get; init; } = string.Empty;

    /// <summary>Optional default branch; falls back to the project's default branch when empty.</summary>
    [JsonPropertyName("defaultBranch")]
    public string DefaultBranch { get; init; } = string.Empty;

    /// <summary>Work item provider of this repository (US5.2); null when none is configured.</summary>
    [JsonPropertyName("workItems")]
    public WorkItemSource? WorkItems { get; init; }

    /// <summary>GitHub Actions follow-up of this repository (US7.1); null when not followed.</summary>
    [JsonPropertyName("github")]
    public GitHubTracking? GitHub { get; init; }

    /// <summary>Time tracking rules for this repository; null values keep the global ones (US5.3).</summary>
    [JsonPropertyName("timeTracking")]
    public TimeTrackingOverrides? TimeTracking { get; init; }
}
