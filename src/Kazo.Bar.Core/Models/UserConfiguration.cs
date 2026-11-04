namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The single per-user configuration file (US2.3): application settings and every
/// project with its repositories. Stored as <c>%APPDATA%\DevToolbar\config.json</c>.
/// Never holds secrets: providers keep a reference to a secret store entry (US2.4).
/// </summary>
public sealed class UserConfiguration
{
    /// <summary>Schema version written by this build. Bump it and add a migration when the format changes (US2.5).</summary>
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("activeProjectId")]
    public string? ActiveProjectId { get; set; }

    /// <summary>Application-wide settings (toolbar placement, display...).</summary>
    [JsonPropertyName("settings")]
    public GlobalSettings Settings { get; set; } = new();

    [JsonPropertyName("projects")]
    public List<ProjectConfig> Projects { get; set; } = new();
}
