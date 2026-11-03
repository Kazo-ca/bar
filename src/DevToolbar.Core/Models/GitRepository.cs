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
}
