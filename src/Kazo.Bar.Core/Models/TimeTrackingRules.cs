namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Global time tracking rules (US5.3, US5.5), in the <c>settings.timeTracking</c> block of
/// <c>config.json</c>. A repository can override some of them (<see cref="TimeTrackingOverrides"/>).
/// </summary>
public sealed class TimeTrackingRules
{
    public const int DefaultIdleMinutes = 15;

    /// <summary>Minutes without keyboard or mouse input before the timer freezes.</summary>
    [JsonPropertyName("idleMinutes")]
    public int IdleMinutes { get; set; } = DefaultIdleMinutes;

    /// <summary>Locking the Windows session freezes the timer immediately.</summary>
    [JsonPropertyName("pauseOnLock")]
    public bool PauseOnLock { get; set; } = true;

    /// <summary>Lunch window: an absence inside it is counted as lunch automatically.</summary>
    [JsonPropertyName("lunch")]
    public LunchRule Lunch { get; set; } = new();

    /// <summary>Recurring daily meeting: an absence during it is counted as that meeting.</summary>
    [JsonPropertyName("dailyMeeting")]
    public DailyMeetingRule DailyMeeting { get; set; } = new();

    /// <summary>Applies the repository overrides (the repository wins) and returns a new instance.</summary>
    public TimeTrackingRules With(TimeTrackingOverrides? overrides)
    {
        if (overrides == null)
            return Clone();
        return new TimeTrackingRules
        {
            IdleMinutes = overrides.IdleMinutes is > 0 ? overrides.IdleMinutes.Value : IdleMinutes,
            PauseOnLock = overrides.PauseOnLock ?? PauseOnLock,
            Lunch = (overrides.Lunch ?? Lunch).Clone(),
            DailyMeeting = (overrides.DailyMeeting ?? DailyMeeting).Clone(),
        };
    }

    public TimeTrackingRules Clone() => new()
    {
        IdleMinutes = IdleMinutes,
        PauseOnLock = PauseOnLock,
        Lunch = Lunch.Clone(),
        DailyMeeting = DailyMeeting.Clone(),
    };
}

/// <summary>Per-repository overrides (<c>repositories[].timeTracking</c>). Null keeps the global value.</summary>
public sealed class TimeTrackingOverrides
{
    [JsonPropertyName("idleMinutes")]
    public int? IdleMinutes { get; set; }

    [JsonPropertyName("pauseOnLock")]
    public bool? PauseOnLock { get; set; }

    [JsonPropertyName("lunch")]
    public LunchRule? Lunch { get; set; }

    [JsonPropertyName("dailyMeeting")]
    public DailyMeetingRule? DailyMeeting { get; set; }
}

/// <summary>Lunch window (default 12:00 to 13:00).</summary>
public sealed class LunchRule
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("start")]
    public TimeOnly Start { get; set; } = new(12, 0);

    [JsonPropertyName("end")]
    public TimeOnly End { get; set; } = new(13, 0);

    /// <summary>
    /// The absence must cover at least this many minutes of the window to count as lunch,
    /// so a short coffee break at 12:10 is not taken for lunch.
    /// </summary>
    [JsonPropertyName("minimumMinutes")]
    public int MinimumMinutes { get; set; } = 30;

    public LunchRule Clone() => new() { Enabled = Enabled, Start = Start, End = End, MinimumMinutes = MinimumMinutes };
}

/// <summary>Recurring meeting, for example the daily stand-up 11:45 to 12:00 (off by default).</summary>
public sealed class DailyMeetingRule
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "Daily";

    [JsonPropertyName("start")]
    public TimeOnly Start { get; set; } = new(11, 45);

    [JsonPropertyName("end")]
    public TimeOnly End { get; set; } = new(12, 0);

    /// <summary>Days the meeting takes place (default Monday to Friday).</summary>
    [JsonPropertyName("days")]
    public List<DayOfWeek> Days { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public DailyMeetingRule Clone() => new() { Enabled = Enabled, Label = Label, Start = Start, End = End, Days = [.. Days] };
}
