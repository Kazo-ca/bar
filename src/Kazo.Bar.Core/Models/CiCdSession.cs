namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a CI/CD pipeline session (e.g., GitHub Actions workflow run).
/// </summary>
public class CiCdSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public CiCdStatus Status { get; set; }

    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("branch")]
    public string Branch { get; set; } = string.Empty;

    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }
}

/// <summary>
/// Status of a CI/CD session.
/// </summary>
public enum CiCdStatus
{
    Queued,
    InProgress,
    Completed
}
