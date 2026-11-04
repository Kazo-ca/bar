namespace Kazo.Bar.Tests.Unit.TimeTracking;

using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Storage;
using Kazo.Bar.Infrastructure.TimeTracking;
using Kazo.Bar.Tests.Unit.Infrastructure;

[TestFixture]
public class TimeTrackingServiceTests
{
    private static readonly DateTime Day = new(2026, 11, 3);
    private static DateTime At(int h, int m) => Day.AddHours(h).AddMinutes(m);

    private TempDirectory _temp = null!;
    private ManualClock _clock = null!;
    private FakePresence _presence = null!;
    private FakeGlobalSettings _global = null!;
    private FakeProjects _projects = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempDirectory();
        _clock = new ManualClock(At(8, 10));
        _presence = new FakePresence(_clock);
        _global = new FakeGlobalSettings();
        _global.Current.TimeTracking.DailyMeeting = new DailyMeetingRule { Enabled = true, Label = "Daily", Start = new(11, 45), End = new(12, 0) };
        _projects = new FakeProjects();
    }

    [TearDown]
    public void TearDown() => _temp.Dispose();

    private async Task<TimeTrackingService> CreateAsync()
    {
        var service = new TimeTrackingService(new JsonStorageService(_temp.Paths), _presence, _global, _projects, _clock);
        await service.InitializeAsync(startPolling: false);
        return service;
    }

    /// <summary>Moves the clock, the user doing nothing, with a presence check every minute.</summary>
    private void IdleUntil(TimeTrackingService service, DateTime until)
    {
        while (_clock.GetUtcNow().UtcDateTime < until)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            service.Poll();
        }
    }

    [Test]
    public async Task Startup_RecordsTheFirstActivityOfTheDay()
    {
        using var service = await CreateAsync();

        Assert.That(service.Today!.FirstActivity, Is.EqualTo(At(8, 10)));
        Assert.That(service.Today.Date, Is.EqualTo(DateOnly.FromDateTime(Day)));
    }

    [Test]
    public async Task Idle_FreezesTheTimerAtTheLastInput_AndResumesOnReturn()
    {
        using var service = await CreateAsync();
        service.Start("alpha", "42");
        _clock.Set(At(10, 0));
        _presence.Input();
        var detected = 0;
        service.OnIdleDetected += () => detected++;

        IdleUntil(service, At(10, 20));

        Assert.That(service.IsIdlePaused, Is.True);
        Assert.That(service.AwaySince, Is.EqualTo(At(10, 0)), "the absence starts at the last input, not when it is detected");
        Assert.That(service.GetActive(), Is.Null);
        Assert.That(detected, Is.EqualTo(1));
        var frozen = service.GetTodayTotal("alpha");

        IReadOnlyList<Absence>? returned = null;
        service.OnAbsenceEnded += a => returned = a;
        _clock.Set(At(10, 40));
        _presence.Input();
        service.Poll();

        Assert.That(service.IsIdlePaused, Is.False);
        Assert.That(frozen, Is.EqualTo(TimeSpan.FromMinutes(110)), "08:10 to 10:00");
        Assert.That(returned, Is.Not.Null);
        Assert.That(returned!.Single().Start, Is.EqualTo(At(10, 0)));
        Assert.That(returned.Single().End, Is.EqualTo(At(10, 40)));
        Assert.That(returned.Single().Kind, Is.EqualTo(AbsenceKind.Unclassified));
        var active = service.GetActive()!;
        Assert.That((active.ProjectId, active.WorkItemId, active.StartTime), Is.EqualTo(("alpha", "42", At(10, 40))));
    }

    [Test]
    public async Task LockDuringDailyAndLunch_IsSplitIntoMeetingAndLunch()
    {
        using var service = await CreateAsync();
        service.Start("alpha");
        _clock.Set(At(11, 45));
        _presence.Lock();

        Assert.That(service.IsIdlePaused, Is.True);
        Assert.That(service.Today!.LastLock, Is.EqualTo(At(11, 45)));

        _clock.Set(At(13, 0));
        _presence.Unlock();

        var pending = service.PendingAbsences;
        Assert.That(pending.Select(a => (a.Kind, a.Start, a.End)), Is.EqualTo(new[]
        {
            (AbsenceKind.Meeting, At(11, 45), At(12, 0)),
            (AbsenceKind.Lunch, At(12, 0), At(13, 0)),
        }));
        Assert.That(pending.All(a => a.Cause == AbsenceCause.Locked));
        // 08:10-11:45 of timer + the 15 min daily, lunch not counted.
        Assert.That(service.GetTodayTotal("alpha"), Is.EqualTo(TimeSpan.FromMinutes(215 + 15)));
    }

    [Test]
    public async Task WorkedThroughLunch_CountsAsWork()
    {
        using var service = await CreateAsync();
        service.Start("alpha");
        _clock.Set(At(12, 0));
        _presence.Lock();
        _clock.Set(At(13, 0));
        _presence.Unlock();
        var lunch = service.PendingAbsences.Single(a => a.Kind == AbsenceKind.Lunch);
        var before = service.GetTodayTotal("alpha");

        await service.ClassifyAbsenceAsync(lunch.Id, AbsenceKind.Worked);

        Assert.That(service.GetTodayTotal("alpha") - before, Is.EqualTo(TimeSpan.FromHours(1)));
        Assert.That(service.PendingAbsences, Is.Empty);
        var entries = await service.GetEntriesAsync(Day, "alpha");
        Assert.That(entries.Single(e => e.AbsenceKind == AbsenceKind.Worked).Description, Is.EqualTo("Worked through lunch"));
    }

    [Test]
    public async Task OtherWithLabel_AndRemoved()
    {
        using var service = await CreateAsync();
        service.Start("alpha");
        _clock.Set(At(14, 0));
        _presence.Lock();
        _clock.Set(At(14, 30));
        _presence.Unlock();
        _clock.Set(At(15, 0));
        _presence.Lock();
        _clock.Set(At(15, 10));
        _presence.Unlock();
        var pending = service.PendingAbsences;

        await service.ClassifyAbsenceAsync(pending[0].Id, AbsenceKind.Other, "  Call with the client ");
        await service.ClassifyAbsenceAsync(pending[1].Id, AbsenceKind.Removed);

        var entries = await service.GetEntriesAsync(Day);
        Assert.That(entries.Where(e => e.AbsenceKind != null).Select(e => e.Description), Is.EqualTo(new[] { "Call with the client" }));
        Assert.That(service.Today!.Absences.Select(a => a.Kind), Is.EqualTo(new[] { AbsenceKind.Other, AbsenceKind.Removed }));
    }

    [Test]
    public async Task ShortLock_IsNotAnAbsence()
    {
        using var service = await CreateAsync();
        service.Start("alpha");
        _clock.Set(At(9, 0));
        _presence.Lock();
        _clock.Set(At(9, 0).AddSeconds(30));
        _presence.Unlock();

        Assert.That(service.Today!.Absences, Is.Empty);
        Assert.That(service.GetActive(), Is.Not.Null);
    }

    [Test]
    public async Task IdleWithoutTimer_StillRecordsTheAbsence_AndDoesNotStartATimer()
    {
        using var service = await CreateAsync();
        _clock.Set(At(9, 0));
        _presence.Input();
        IdleUntil(service, At(9, 30));
        _presence.Input();
        service.Poll();

        Assert.That(service.Today!.Absences.Single().Start, Is.EqualTo(At(9, 0)));
        Assert.That(service.GetActive(), Is.Null);
    }

    [Test]
    public async Task IdleDelay_ComesFromTheRepository_WhenItSetsOne()
    {
        _global.Current.TimeTracking.IdleMinutes = 30;
        using var service = await CreateAsync();
        Assert.That(service.IdleTimeout, Is.EqualTo(TimeSpan.FromMinutes(30)));

        _projects.Active!.GitRepositories.Add(new GitRepository { Name = "api", TimeTracking = new TimeTrackingOverrides { IdleMinutes = 5 } });

        Assert.That(service.IdleTimeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
        service.Start("alpha");
        _clock.Set(At(9, 0));
        _presence.Input();
        IdleUntil(service, At(9, 6));
        Assert.That(service.IsIdlePaused, Is.True);
    }

    [Test]
    public async Task PauseOnLockOff_KeepsTheTimerRunning_ButRecordsTheLock()
    {
        _global.Current.TimeTracking.PauseOnLock = false;
        using var service = await CreateAsync();
        service.Start("alpha");
        _clock.Set(At(17, 5));
        _presence.Lock();

        Assert.That(service.IsIdlePaused, Is.False);
        Assert.That(service.Today!.LastLock, Is.EqualTo(At(17, 5)));
    }

    [Test]
    public async Task LockedOvernight_EndsTheDay_AndStartsTheNextOne()
    {
        using var service = await CreateAsync();
        service.Start("alpha");
        _clock.Set(At(17, 5));
        _presence.Lock();
        var tomorrow = Day.AddDays(1);
        _clock.Set(tomorrow.AddHours(8).AddMinutes(2));
        _presence.Unlock();

        Assert.That(service.Today!.Date, Is.EqualTo(DateOnly.FromDateTime(tomorrow)));
        Assert.That(service.Today.FirstActivity, Is.EqualTo(tomorrow.AddHours(8).AddMinutes(2)));
        Assert.That(service.Today.Absences, Is.Empty, "the night is not an absence");
        Assert.That(service.GetActive()!.StartTime, Is.EqualTo(tomorrow.AddHours(8).AddMinutes(2)), "a frozen timer carries on");

        var days = await service.GetWorkDaysAsync(DateOnly.FromDateTime(Day), DateOnly.FromDateTime(tomorrow));
        Assert.That(days.Select(d => d.Date), Is.EqualTo(new[] { DateOnly.FromDateTime(Day), DateOnly.FromDateTime(tomorrow) }));
        Assert.That(days[0].LastLock, Is.EqualTo(At(17, 5)));
        Assert.That(days[0].Entries.Single().EndTime, Is.EqualTo(At(17, 5)));
    }

    [Test]
    public async Task IdleOvernightWithoutLock_EndsTheDayAtTheLastInput()
    {
        using var service = await CreateAsync();
        _clock.Set(At(18, 0));
        _presence.Input();
        IdleUntil(service, Day.AddDays(1).AddMinutes(5));
        _clock.Set(Day.AddDays(1).AddHours(8));
        _presence.Input();
        service.Poll();

        var days = await service.GetWorkDaysAsync(DateOnly.FromDateTime(Day), DateOnly.FromDateTime(Day.AddDays(1)));
        Assert.That(days[0].LastLock, Is.EqualTo(At(18, 0)));
        Assert.That(days[1].FirstActivity, Is.EqualTo(Day.AddDays(1).AddHours(8)));
        Assert.That(days[1].Absences, Is.Empty);
    }

    [Test]
    public async Task TimerRunningPastMidnight_IsCutInTwo()
    {
        using var service = await CreateAsync();
        _clock.Set(At(23, 30));
        service.Start("alpha");
        _clock.Set(Day.AddDays(1).AddMinutes(10));
        _presence.Input();
        service.Poll();

        Assert.That(service.GetActive()!.StartTime, Is.EqualTo(Day.AddDays(1)));
        var days = await service.GetWorkDaysAsync(DateOnly.FromDateTime(Day), DateOnly.FromDateTime(Day.AddDays(1)));
        Assert.That(days[0].Entries.Single().EndTime, Is.EqualTo(Day.AddDays(1)));
    }

    [Test]
    public async Task DataSurvivesARestart()
    {
        var service = await CreateAsync();
        service.Start("alpha", "42");
        _clock.Set(At(10, 0));
        service.Stop();
        await service.ShutdownAsync();
        service.Dispose();

        _clock.Set(At(10, 30));
        using var reloaded = await CreateAsync();

        var today = reloaded.Today!;
        Assert.That(today.FirstActivity, Is.EqualTo(At(8, 10)));
        Assert.That(today.Entries.Single().EndTime, Is.EqualTo(At(10, 0)));
        Assert.That(reloaded.GetTodayTotal("alpha"), Is.EqualTo(TimeSpan.FromMinutes(110)));
        Assert.That(reloaded.GetActive(), Is.Null);
        Assert.That(File.Exists(Path.Combine(_temp.Paths.DataDirectory, "time-2026-11.json")));
    }

    [Test]
    public async Task Crash_ClosesTheEntryAtTheLastHeartbeat_AndAsksAboutTheGap()
    {
        var service = await CreateAsync();
        service.Start("alpha", "42");
        _clock.Set(At(9, 0));
        await service.PersistAsync(); // heartbeat at 09:00, then the process dies
        service.Dispose();

        _clock.Set(At(9, 20));
        using var reloaded = await CreateAsync();

        var today = reloaded.Today!;
        Assert.That(today.Entries[0].EndTime, Is.EqualTo(At(9, 0)));
        Assert.That(today.Absences.Single().Cause, Is.EqualTo(AbsenceCause.AppClosed));
        Assert.That(today.Absences.Single().Duration, Is.EqualTo(TimeSpan.FromMinutes(20)));
        Assert.That(reloaded.GetActive()!.WorkItemId, Is.EqualTo("42"), "the timer carries on after a restart the same day");
    }

    [Test]
    public async Task StopWhileAway_DoesNotRestartOnReturn()
    {
        using var service = await CreateAsync();
        service.Start("alpha");
        _clock.Set(At(9, 0));
        _presence.Lock();
        service.Stop();
        _clock.Set(At(9, 30));
        _presence.Unlock();

        Assert.That(service.GetActive(), Is.Null);
    }

    [Test]
    public async Task ClassifyingAnUnknownAbsence_Throws()
    {
        using var service = await CreateAsync();
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.ClassifyAbsenceAsync("nope", AbsenceKind.Meeting));
    }
}
