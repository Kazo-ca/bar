namespace DevToolbar.Tests.Unit.TimeTracking;

using System.Text.Json;
using DevToolbar.Core.Models;
using DevToolbar.Core.TimeTracking;

[TestFixture]
public class AbsenceClassifierTests
{
    // Tuesday.
    private static readonly DateTime Day = new(2026, 11, 3);

    private static TimeTrackingRules RulesWithDaily() => new()
    {
        DailyMeeting = new DailyMeetingRule { Enabled = true, Label = "Daily", Start = new(11, 45), End = new(12, 0) },
    };

    private static DateTime At(int h, int m) => Day.AddHours(h).AddMinutes(m);

    [Test]
    public void AwayFromDailyToEndOfLunch_BecomesMeetingThenLunch()
    {
        var segments = AbsenceClassifier.Split(At(11, 45), At(13, 0), RulesWithDaily(), AbsenceCause.Locked, "alpha", "42");

        Assert.That(segments.Select(s => (s.Start, s.End, s.Kind)), Is.EqualTo(new[]
        {
            (At(11, 45), At(12, 0), AbsenceKind.Meeting),
            (At(12, 0), At(13, 0), AbsenceKind.Lunch),
        }));
        Assert.That(segments[0].Label, Is.EqualTo("Daily"));
        Assert.That(segments.All(s => s.Automatic && s.PendingReview && s.ProjectId == "alpha" && s.WorkItemId == "42"));
        Assert.That(segments.Select(s => s.GroupId).Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public void LongerThanLunch_LeavesTheRestToLabel()
    {
        var segments = AbsenceClassifier.Split(At(11, 30), At(13, 20), new TimeTrackingRules(), AbsenceCause.Idle);

        Assert.That(segments.Select(s => (s.Start, s.End, s.Kind)), Is.EqualTo(new[]
        {
            (At(11, 30), At(12, 0), AbsenceKind.Unclassified),
            (At(12, 0), At(13, 0), AbsenceKind.Lunch),
            (At(13, 0), At(13, 20), AbsenceKind.Unclassified),
        }));
        Assert.That(segments.Where(s => s.Kind == AbsenceKind.Unclassified).All(s => !s.Automatic));
    }

    [Test]
    public void ShortBreakAtNoon_IsNotLunch()
    {
        var segments = AbsenceClassifier.Split(At(12, 10), At(12, 25), new TimeTrackingRules(), AbsenceCause.Idle);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(segments[0].Kind, Is.EqualTo(AbsenceKind.Unclassified));
    }

    [Test]
    public void DailyIsSkippedOnDaysItDoesNotHappen()
    {
        var saturday = new DateTime(2026, 11, 7);
        var segments = AbsenceClassifier.Split(saturday.AddHours(11).AddMinutes(45), saturday.AddHours(12), RulesWithDaily(), AbsenceCause.Idle);

        Assert.That(segments.Single().Kind, Is.EqualTo(AbsenceKind.Unclassified));
    }

    [Test]
    public void DisabledLunch_IsNeverApplied()
    {
        var rules = new TimeTrackingRules { Lunch = new LunchRule { Enabled = false } };
        var segments = AbsenceClassifier.Split(At(12, 0), At(13, 0), rules, AbsenceCause.Idle);

        Assert.That(segments.Single().Kind, Is.EqualTo(AbsenceKind.Unclassified));
    }

    [Test]
    public void MeetingOverlappingLunch_KeepsTheOverlap()
    {
        var rules = new TimeTrackingRules
        {
            DailyMeeting = new DailyMeetingRule { Enabled = true, Start = new(11, 45), End = new(12, 15) },
        };
        var segments = AbsenceClassifier.Split(At(11, 45), At(13, 0), rules, AbsenceCause.Idle);

        Assert.That(segments.Select(s => (s.Start, s.End, s.Kind)), Is.EqualTo(new[]
        {
            (At(11, 45), At(12, 15), AbsenceKind.Meeting),
            (At(12, 15), At(13, 0), AbsenceKind.Lunch),
        }));
    }

    [Test]
    public void EmptyOrReversedRange_GivesNothing()
    {
        Assert.That(AbsenceClassifier.Split(At(10, 0), At(10, 0), new TimeTrackingRules(), AbsenceCause.Idle), Is.Empty);
        Assert.That(AbsenceClassifier.Split(At(10, 0), At(9, 0), new TimeTrackingRules(), AbsenceCause.Idle), Is.Empty);
    }

    [Test]
    public void RepositoryOverrides_WinOverGlobalRules()
    {
        var global = RulesWithDaily();
        var effective = global.With(new TimeTrackingOverrides { IdleMinutes = 5, Lunch = new LunchRule { Start = new(11, 30), End = new(12, 30) } });

        Assert.That(effective.IdleMinutes, Is.EqualTo(5));
        Assert.That(effective.Lunch.Start, Is.EqualTo(new TimeOnly(11, 30)));
        Assert.That(effective.DailyMeeting.Enabled, Is.True, "not overridden: global value kept");
        Assert.That(global.IdleMinutes, Is.EqualTo(TimeTrackingRules.DefaultIdleMinutes), "global rules are not changed");
        Assert.That(global.With(new TimeTrackingOverrides { IdleMinutes = 0 }).IdleMinutes, Is.EqualTo(TimeTrackingRules.DefaultIdleMinutes));
    }

    [Test]
    public void Rules_ReadHandWrittenTimes()
    {
        var rules = JsonSerializer.Deserialize<TimeTrackingRules>(
            """{ "idleMinutes": 10, "lunch": { "start": "11:30", "end": "12:30" }, "dailyMeeting": { "enabled": true, "start": "11:45:00", "end": "12:00", "days": [1, 2, 3] } }""")!;

        Assert.That(rules.IdleMinutes, Is.EqualTo(10));
        Assert.That(rules.Lunch.Start, Is.EqualTo(new TimeOnly(11, 30)));
        Assert.That(rules.DailyMeeting.End, Is.EqualTo(new TimeOnly(12, 0)));
        Assert.That(rules.DailyMeeting.Days, Is.EqualTo(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday }));
        Assert.That(rules.PauseOnLock, Is.True, "missing values keep their defaults");
    }
}
