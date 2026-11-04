namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Choices the user made on the toolbar itself (US3.6, US3.7): module order and the width of
/// resizable modules. Saved in the "settings.toolbarLayout" section of config.json. Both are
/// optional: an empty order keeps the default order, a missing width the plugin's default.
/// </summary>
public sealed class ToolbarLayoutSettings
{
    /// <summary>Module ids in the order chosen by the user. Unknown ids are ignored, new modules go at the end of their zone.</summary>
    [JsonPropertyName("moduleOrder")]
    public List<string> ModuleOrder { get; set; } = new();

    /// <summary>Width in CSS px of resizable modules, by module id.</summary>
    [JsonPropertyName("moduleWidths")]
    public Dictionary<string, int> ModuleWidths { get; set; } = new();
}
