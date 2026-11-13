namespace Kazo.Bar.Tests.Unit.TimeTracking;

using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Storage;
using Kazo.Bar.Infrastructure.TimeTracking;
using Kazo.Bar.Tests.Unit.Infrastructure;

/// <summary>US5.9: hours worked per day, and absence labels put on a day after the fact.</summary>
[TestFixture]
public class DayLabelsTests
{
    private static readonly DateTime Day = new(2026, 11, 3);
    private static readonly DateOnly DayDate = DateOnly.FromDateTime(Day);
    private static DateTime At(int h, int m) => Day.AddHours(h).AddMinutes(m);
    private static DateTime Tomorrow(int h, int m) => Day.AddDays(1).AddHours(h).AddMinutes(m);

    private TempDirectory _temp = null!;
    private ManualClock _clock = null!;
    private FakePresence _presence = null!;
    private FakeGlobalSettings _global = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempDirectory();
        _clock = new ManualClock(At(8, 0));
        _presence = new FakePresence(_clock);
        _global = new FakeGlobalSettings();
    }

    [TearDown]
    public void TearDown() => _temp.Dispose();

    private async Task<TimeTrackingService> CreateAsync()
    {
        var service = new TimeTrackingService(new JsonStorageService(_temp.Paths), _presence, _global, new FakeProjects(), _clock);
        await service.InitializeAsync(startPolling: false);
        return service;
    }

    /// <summary>08:00-17:00 on alpha with a lock 10:00-10:30 left unlabelled, then the next morning.</summary>
    private async Task<(TimeTrackingService Service, string AbsenceId)> YesterdayWithAnAbsenceAsync()
    {
        var service = await CreateAsync();
        service.Start("alpha");
        _clock.Set(At(10, 0));
        _presence.Lock();
        _clock.Set(At(10, 30));
        _presence.Unlock();
        var absenceId = service.PendingAbsences.Single().Id;
        _clock.Set(At(17, 0));
        _presence.Lock();
        _clock.Set(Tomorrow(8, 0));
        _presence.Unlock();
        await service.PersistAsync();
        return (service, absenceId);
    }

    [Test]
    public void WorkedTime_AddsTimerAndCountedAbsences_OfAllProjects()
    {
        var day = new WorkDay
        {
            Date = DayDate,
            Entries =
            {
                new TimeEntry { ProjectId = "alpha", StartTime = At(8, 0), EndTime = At(10, 0) },
                new TimeEntry { ProjectId = "beta", StartTime = At(13, 0), EndTime = null },
            },
            Absences =
            {
                new Absence { Start = At(10, 0), End = At(10, 30), Kind = AbsenceKind.Meeting },
                new Absence { Start = At(12, 0), End = At(13, 0), Kind = AbsenceKind.Lunch },
                new Absence { Start = At(11, 0), End = At(11, 15), Kind = AbsenceKind.Unclassified },
            },
        };

        Assert.That(day.WorkedTime(At(14, 0)), Is.EqualTo(TimeSpan.FromHours(3.5)), "2 h + 1 h running + 30 min meeting; lunch and unlabelled not counted");
    }

    [Test]
    public async Task AbsenceOfYesterday_CanBeLabelledTheNextDay()
    {
        var (service, absenceId) = await YesterdayWithAnAbsenceAsync();
        using var _ = service;

        await service.ClassifyAbsenceAsync(DayDate, absenceId, AbsenceKind.Meeting, " Client call ");

        var yesterday = (await service.GetWorkDaysAsync(DayDate, DayDate)).Single();
        var absence = yesterday.Absences.Single();
        Assert.That((absence.Kind, absence.Label, absence.PendingReview), Is.EqualTo((AbsenceKind.Meeting, "Client call", false)));
        Assert.That(yesterday.WorkedTime(Tomorrow(8, 0)), Is.EqualTo(TimeSpan.FromHours(9)), "the meeting now counts: 08:00-17:00");
        var entries = await service.GetEntriesAsync(Day, "alpha");
        Assert.That(entries.Any(e => e.AbsenceKind == AbsenceKind.Meeting && e.Description == "Client call"));
    }

    [Test]
    public async Task LabellingAnUnknownAbsenceOfAPastDay_Throws()
    {
        var (service, _) = await YesterdayWithAnAbsenceAsync();
        using var __ = service;

        Assert.ThrowsAsync<KeyNotFoundException>(() => service.ClassifyAbsenceAsync(DayDate, "nope", AbsenceKind.Other));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.ClassifyAbsenceAsync(DayDate.AddDays(-5), "nope", AbsenceKind.Other));
    }

    [Test]
    public async Task DayOff_OnAPastDayWithNothingRecorded_IsSavedAndSurvivesARestart()
    {
        var service = await CreateAsync();
        var sickDay = DayDate.AddDays(-1);

        await service.SetDayOffAsync(sickDay, new DayOff { Kind = DayOffKind.Sick, Label = "  Flu  " });
        await service.ShutdownAsync();
        service.Dispose();
        using var reloaded = await CreateAsync();

        var day = (await reloaded.GetWorkDaysAsync(sickDay, sickDay)).Single();
        Assert.That((day.DayOff!.Kind, day.DayOff.Label, day.DayOff.Hours), Is.EqualTo((DayOffKind.Sick, "Flu", (double?)null)));
        Assert.That(day.WorkedTime(At(8, 0)), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task DayOff_KeepsWhatWasRecordedThatDay_AndCanBeRemoved()
    {
        var (service, _) = await YesterdayWithAnAbsenceAsync();
        using var __ = service;

        await service.SetDayOffAsync(DayDate, new DayOff { Kind = DayOffKind.Personal, Label = "Dentist", Hours = 1.5 });
        var day = (await service.GetWorkDaysAsync(DayDate, DayDate)).Single();
        Assert.That(day.DayOff!.Hours, Is.EqualTo(1.5));
        Assert.That(day.Entries, Is.Not.Empty, "the timer entries of that day are untouched");
        Assert.That(day.Absences, Has.Count.EqualTo(1));

        await service.SetDayOffAsync(DayDate, null);
        Assert.That((await service.GetWorkDaysAsync(DayDate, DayDate)).Single().DayOff, Is.Null);
    }

    [Test]
    public async Task DayOff_Today_IsKeptInTheDayBeingRecorded()
    {
        using var service = await CreateAsync();
        var changed = 0;
        service.OnTrackingChanged += () => changed++;

        await service.SetDayOffAsync(DayDate, new DayOff { Kind = DayOffKind.Vacation, Hours = 4 });

        Assert.That(service.Today!.DayOff!.Kind, Is.EqualTo(DayOffKind.Vacation));
        Assert.That(changed, Is.EqualTo(1));
        _clock.Set(At(9, 0));
        service.Start("alpha");
        Assert.That(service.Today!.DayOff!.Hours, Is.EqualTo(4));
    }

    [Test]
    public async Task DayOff_InTheFuture_OrWithInvalidHours_IsRefused()
    {
        using var service = await CreateAsync();

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetDayOffAsync(DayDate.AddDays(1), new DayOff()));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetDayOffAsync(DayDate, new DayOff { Hours = 0 }));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetDayOffAsync(DayDate, new DayOff { Hours = 25 }));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetDayOffAsync(DayDate, new DayOff { Kind = (DayOffKind)42 }));
        Assert.That(service.Today!.DayOff, Is.Null);
    }
}
