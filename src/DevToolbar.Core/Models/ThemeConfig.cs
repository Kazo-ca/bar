namespace DevToolbar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Theme configuration for a project.
/// </summary>
public class ThemeConfig
{
    /// <summary>
    /// Theme of the toolbar (US3.3): "light", "dark" or "green" (<see cref="ThemeNames"/>).
    /// A theme only changes styles, never the elements. Unknown names fall back to light.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = ThemeNames.Light;

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

/// <summary>
/// Themes offered to the user (US3.3), from doc/Toolbar-desing-exemple.png.
/// </summary>
public static class ThemeNames
{
    public const string Light = "light";
    public const string Dark = "dark";
    public const string Green = "green";

    /// <summary>All themes, in the order shown in Settings.</summary>
    public static IReadOnlyList<string> All { get; } = [Light, Dark, Green];

    /// <summary>Returns a known theme name (lower case), or <see cref="Light"/> for anything else.</summary>
    public static string Normalize(string? name)
    {
        var trimmed = name?.Trim().ToLowerInvariant();
        return trimmed is not null && All.Contains(trimmed) ? trimmed : Light;
    }

    /// <summary>Label shown in Settings.</summary>
    public static string DisplayName(string name) => Normalize(name) switch
    {
        Dark => "Dark",
        Green => "Green",
        _ => "Light",
    };
}
