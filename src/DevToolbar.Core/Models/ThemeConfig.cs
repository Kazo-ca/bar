namespace DevToolbar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Theme configuration for a project.
/// </summary>
public class ThemeConfig
{
    [JsonPropertyName("accentColor")]
    public string AccentColor { get; set; } = "#0078D7";

    [JsonPropertyName("fontFamily")]
    public string FontFamily { get; set; } = "'Segoe UI', sans-serif";

    /// <summary>
    /// Optional custom CSS injected into the toolbar after the theme variables (US3.2).
    /// </summary>
    [JsonPropertyName("customCss")]
    public string? CustomCss { get; set; }
}
