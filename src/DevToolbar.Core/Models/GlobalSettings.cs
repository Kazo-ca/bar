namespace DevToolbar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Application-wide settings for toolbar behavior, positioning, and display.
/// Persisted in the "settings" section of the user configuration (%APPDATA%\DevToolbar\config.json).
/// </summary>
public class GlobalSettings
{
    /// <summary>
    /// How the toolbar positions itself on the target monitor.
    /// </summary>
    [JsonPropertyName("toolbarBehavior")]
    public ToolbarBehavior ToolbarBehavior { get; set; } = ToolbarBehavior.Floating;

    /// <summary>
    /// Index of the target monitor (0 = primary, 1 = secondary, etc.).
    /// </summary>
    [JsonPropertyName("targetMonitorIndex")]
    public int TargetMonitorIndex { get; set; } = 0;

    /// <summary>
    /// Whether to show a button per open window in the toolbar (US8.5).
    /// </summary>
    [JsonPropertyName("showActiveWindows")]
    public bool ShowActiveWindows { get; set; } = false;

    /// <summary>
    /// Height of the toolbar in pixels when docked.
    /// </summary>
    [JsonPropertyName("toolbarHeight")]
    public int ToolbarHeight { get; set; } = 56;

    /// <summary>
    /// Polling interval in milliseconds for refreshing the open windows list.
    /// </summary>
    [JsonPropertyName("windowPollingIntervalMs")]
    public int WindowPollingIntervalMs { get; set; } = 1500;

    /// <summary>
    /// Where access tokens are stored (US2.4). Changing it moves the existing secrets.
    /// </summary>
    [JsonPropertyName("secretStore")]
    public SecretStoreKind SecretStore { get; set; } = SecretStoreKind.CredentialManager;

    /// <summary>How often GitHub Actions runs are polled, in seconds (US7.1). Minimum 30.</summary>
    [JsonPropertyName("ciPollingIntervalSeconds")]
    public int CiPollingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Inactivity, lunch and daily meeting rules (US5.3, US5.5). A repository can override them
    /// (<see cref="GitRepository.TimeTracking"/>).
    /// </summary>
    [JsonPropertyName("timeTracking")]
    public TimeTrackingRules TimeTracking { get; set; } = new();

    /// <summary>Module order and widths chosen on the toolbar (US3.6, US3.7).</summary>
    [JsonPropertyName("toolbarLayout")]
    public ToolbarLayoutSettings ToolbarLayout { get; set; } = new();
}
