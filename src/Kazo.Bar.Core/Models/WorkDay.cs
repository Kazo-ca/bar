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

    /// <summary>Absence label for the day (sick, vacation...), set from the time report (US5.9).</summary>
    [JsonPropertyName("dayOff")]
    public DayOff? DayOff { get; set; }

    /// <summary>
    /// Hours worked this day, all projects together: timer entries (a running one up to
    /// <paramref name="now"/>) plus the absences counted as work (meeting, worked, other).
    /// </summary>
    public TimeSpan WorkedTime(DateTime now)
    {
        var timer = Entries.Aggregate(TimeSpan.Zero, (sum, e) =>
        {
            var end = e.EndTime ?? now;
            return end > e.StartTime ? sum + (end - e.StartTime) : sum;
        });
        var counted = Absences.Where(a => a.CountsAsWork).Aggregate(TimeSpan.Zero, (sum, a) => sum + a.Duration);
        return timer + counted;
    }

    /// <summary>Nothing recorded and no label: not worth showing.</summary>
    [JsonIgnore]
    public bool IsEmpty => FirstActivity == null && Entries.Count == 0 && Absences.Count == 0 && DayOff == null;
}
