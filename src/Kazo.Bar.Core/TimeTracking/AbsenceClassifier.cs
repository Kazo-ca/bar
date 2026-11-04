namespace Kazo.Bar.Core.TimeTracking;

using Kazo.Bar.Core.Models;

/// <summary>
/// Cuts an absence into segments using the work day rules (US5.5): the part that overlaps the
/// daily meeting becomes a meeting, the part inside the lunch window becomes lunch (when it is
/// long enough), the rest is left for the user to label.
/// Example with a 11:45-12:00 daily and a 12:00-13:00 lunch: away 11:45 to 13:00 gives
/// meeting 11:45-12:00 then lunch 12:00-13:00.
/// </summary>
public static class AbsenceClassifier
{
    public static IReadOnlyList<Absence> Split(DateTime start, DateTime end, TimeTrackingRules rules, AbsenceCause cause,
        string? projectId = null, string? workItemId = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (end <= start)
            return [];

        var groupId = Guid.NewGuid().ToString("N");
        var windows = RuleWindows(start, end, rules);

        var segments = new List<Absence>();
        var cursor = start;
        foreach (var window in windows)
        {
            if (window.Start > cursor)
                segments.Add(Segment(cursor, window.Start, AbsenceKind.Unclassified, null, automatic: false));
            segments.Add(Segment(window.Start, window.End, window.Kind, window.Label, automatic: true));
            cursor = window.End;
        }
        if (cursor < end)
            segments.Add(Segment(cursor, end, AbsenceKind.Unclassified, null, automatic: false));

        return segments;

        Absence Segment(DateTime s, DateTime e, AbsenceKind kind, string? label, bool automatic) => new()
        {
            GroupId = groupId,
            Start = s,
            End = e,
            Kind = kind,
            Label = label,
            Cause = cause,
            Automatic = automatic,
            PendingReview = true,
            ProjectId = projectId,
            WorkItemId = workItemId,
        };
    }

    private sealed record Window(DateTime Start, DateTime End, AbsenceKind Kind, string? Label);

    /// <summary>Meeting and lunch windows clipped to the absence, without overlaps, in order.</summary>
    private static List<Window> RuleWindows(DateTime start, DateTime end, TimeTrackingRules rules)
    {
        var windows = new List<Window>();
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            var meeting = rules.DailyMeeting;
            if (meeting.Enabled && meeting.Days.Contains(day.DayOfWeek)
                && Clip(day, meeting.Start, meeting.End, start, end) is { } m)
            {
                windows.Add(new Window(m.Start, m.End, AbsenceKind.Meeting, string.IsNullOrWhiteSpace(meeting.Label) ? "Meeting" : meeting.Label));
            }

            var lunch = rules.Lunch;
            if (lunch.Enabled && Clip(day, lunch.Start, lunch.End, start, end) is { } l
                && l.End - l.Start >= TimeSpan.FromMinutes(Math.Max(0, lunch.MinimumMinutes)))
            {
                windows.Add(new Window(l.Start, l.End, AbsenceKind.Lunch, null));
            }
        }

        // Where two windows overlap, the earlier one keeps the overlap (a 11:45-12:15 daily pushes lunch to 12:15).
        windows.Sort((a, b) => a.Start.CompareTo(b.Start));
        var result = new List<Window>();
        foreach (var w in windows)
        {
            var s = result.Count > 0 && result[^1].End > w.Start ? result[^1].End : w.Start;
            if (s < w.End)
                result.Add(w with { Start = s });
        }
        return result;
    }

    private static (DateTime Start, DateTime End)? Clip(DateTime day, TimeOnly from, TimeOnly to, DateTime start, DateTime end)
    {
        if (to <= from)
            return null;
        var ws = day + from.ToTimeSpan();
        var we = day + to.ToTimeSpan();
        var s = ws > start ? ws : start;
        var e = we < end ? we : end;
        return e > s ? (s, e) : null;
    }
}
