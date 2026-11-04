namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>How an absence is counted (US5.3, US5.5).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AbsenceKind>))]
public enum AbsenceKind
{
    /// <summary>Not labelled yet: the user is asked on return. Not counted as work.</summary>
    Unclassified,
    /// <summary>A meeting (the daily, or another one). Counted as work.</summary>
    Meeting,
    /// <summary>Lunch. Not counted.</summary>
    Lunch,
    /// <summary>The user worked away from the keyboard (for example through lunch). Counted as work.</summary>
    Worked,
    /// <summary>Something else, described by <see cref="Absence.Label"/>. Counted as work.</summary>
    Other,
    /// <summary>Removed from the timesheet (personal break). Not counted.</summary>
    Removed,
}

/// <summary>Why the timer froze.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AbsenceCause>))]
public enum AbsenceCause
{
    Idle,
    Locked,
    /// <summary>The application was not running (crash, restart) during the day.</summary>
    AppClosed,
}

/// <summary>
/// One period away from the keyboard during a work day. A single absence (11:45 to 13:00) is
/// split into segments that share <see cref="GroupId"/>: daily meeting, then lunch, then the rest.
/// </summary>
public sealed class Absence
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Segments cut from the same absence share this id.</summary>
    [JsonPropertyName("groupId")]
    public string GroupId { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("kind")]
    public AbsenceKind Kind { get; set; }

    /// <summary>Free text ("Daily", "Call with the client"...).</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("cause")]
    public AbsenceCause Cause { get; set; }

    /// <summary>The kind was set by a rule (lunch window, daily meeting), not by the user.</summary>
    [JsonPropertyName("automatic")]
    public bool Automatic { get; set; }

    /// <summary>The user has not confirmed or labelled this segment yet.</summary>
    [JsonPropertyName("pendingReview")]
    public bool PendingReview { get; set; } = true;

    /// <summary>Project and work item whose timer was running when the user left (counted time goes there).</summary>
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("workItemId")]
    public string? WorkItemId { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => End - Start;

    /// <summary>Meeting, worked and other segments count as work time.</summary>
    [JsonIgnore]
    public bool CountsAsWork => IsCounted(Kind);

    public static bool IsCounted(AbsenceKind kind) =>
        kind is AbsenceKind.Meeting or AbsenceKind.Worked or AbsenceKind.Other;
}
