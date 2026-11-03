namespace DevToolbar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// POCO representing a project configuration, loadable from JSON.
/// </summary>
public class ProjectConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("projectType")]
    public string ProjectType { get; set; } = string.Empty;

    [JsonPropertyName("theme")]
    public ThemeConfig Theme { get; set; } = new();

    [JsonPropertyName("enabledPlugins")]
    public List<string> EnabledPlugins { get; set; } = new();

    [JsonPropertyName("actions")]
    public List<ActionConfig> Actions { get; set; } = new();

    [JsonPropertyName("defaultBranch")]
    public string DefaultBranch { get; set; } = "main";

    [JsonPropertyName("repositoryUrl")]
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Git repositories managed by the Git plugin. When empty, the plugin falls back
    /// to a single repository at <see cref="Path"/>.
    /// </summary>
    [JsonPropertyName("repositories")]
    public List<GitRepository> GitRepositories { get; set; } = new();
}
