namespace DevToolbar.Plugins.TimeTracker;

/// <summary>Display formats of the Time Tracker panel.</summary>
public static class TimeFormat
{
    /// <summary>01:05:09</summary>
    public static string Clock(TimeSpan duration) =>
        $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";

    /// <summary>"1 h 15", "45 min"</summary>
    public static string Short(TimeSpan duration)
    {
        var minutes = (int)Math.Round(duration.TotalMinutes);
        return minutes >= 60 ? $"{minutes / 60} h {minutes % 60:D2}" : $"{minutes} min";
    }

    /// <summary>"08:12", or a dash when not recorded yet.</summary>
    public static string Time(DateTime? at) => at?.ToString("HH:mm") ?? "—";
}
