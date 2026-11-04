namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Everything recorded for one calendar day (US5.5): when the session was first opened, when it
/// was last locked, the timer entries and the absences. Stored as one value per day.
/// </summary>
public sealed class WorkDay
{
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }

    /// <summary>First activity of the day (session opened or unlocked, or application started).</summary>
    [JsonPropertyName("firstActivity")]
    public DateTime? FirstActivity { get; set; }

    /// <summary>Last lock (or application exit) of the day; moves forward each time.</summary>
    [JsonPropertyName("lastLock")]
    public DateTime? LastLock { get; set; }

    [JsonPropertyName("entries")]
    public List<TimeEntry> Entries { get; set; } = new();

    [JsonPropertyName("absences")]
    public List<Absence> Absences { get; set; } = new();
}
