namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Configuration for a quick action button.
/// </summary>
public class ActionConfig
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("processPath")]
    public string ProcessPath { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;

    [JsonPropertyName("windowTitleRegex")]
    public string? WindowTitleRegex { get; set; }

    [JsonPropertyName("actionType")]
    public ActionType ActionType { get; set; } = ActionType.Process;

    /// <summary>
    /// Interpreter to use when <see cref="ActionType"/> is <see cref="ActionType.Script"/>.
    /// Examples: "pwsh", "bash", "python".
    /// </summary>
    [JsonPropertyName("interpreter")]
    public string? Interpreter { get; set; }
}

/// <summary>
/// Type of action to perform.
/// </summary>
public enum ActionType
{
    Process,
    Script
}
