using Kazo.Bar.Updates;
using static Kazo.Bar.Tests.Unit.Updates.UpdateTestData;

namespace Kazo.Bar.Tests.Unit.Updates;

public class PeriodicUpdateCheckTests
{
    private static AvailableUpdate Update(string tag)
    {
        var release = Release(tag);
        return new AvailableUpdate(release, ReleaseVersion.Parse(tag), release.Assets[0]);
    }

    [Test]
    public async Task No_internet_is_logged_and_never_thrown()
    {
        var log = new List<string>();
        var check = new PeriodicUpdateCheck(_ => throw new HttpRequestException("No such host is known."), log.Add);

        await check.CheckOnceAsync();

        Assert.That(log, Has.One.Contains("Update check skipped").And.Contains("No such host"));
        Assert.That(check.Latest, Is.Null);
    }

    [Test]
    public async Task Timeouts_and_faulty_handlers_are_swallowed_too()
    {
        var log = new List<string>();
        var timeout = new PeriodicUpdateCheck(_ => throw new TaskCanceledException("timeout"), log.Add);
        var faulty = new PeriodicUpdateCheck(_ => Task.FromResult<AvailableUpdate?>(Update("v0.2.0")), log.Add);
        faulty.UpdateAvailable += _ => throw new InvalidOperationException("UI gone");

        await timeout.CheckOnceAsync();
        await faulty.CheckOnceAsync();

        Assert.That(log.Count(l => l.StartsWith("Update check skipped")), Is.EqualTo(2));
    }

    [Test]
    public async Task Announces_each_new_release_once()
    {
        var results = new Queue<AvailableUpdate?>([Update("v0.2.0"), Update("v0.2.0"), null, Update("v0.3.0")]);
        var check = new PeriodicUpdateCheck(_ => Task.FromResult(results.Dequeue()), _ => { });
        var announced = new List<string>();
        check.UpdateAvailable += u => announced.Add(u.Release.Tag);

        for (var i = 0; i < 4; i++) await check.CheckOnceAsync();

        Assert.That(announced, Is.EqualTo(new[] { "v0.2.0", "v0.3.0" }));
        Assert.That(check.Latest!.Release.Tag, Is.EqualTo("v0.3.0"));
    }

    [Test]
    public async Task Checks_after_the_first_delay_then_on_every_interval()
    {
        var calls = 0;
        var enough = new TaskCompletionSource();
        await using var check = new PeriodicUpdateCheck(_ =>
        {
            if (Interlocked.Increment(ref calls) >= 3) enough.TrySetResult();
            throw new HttpRequestException("offline");
        }, _ => { }, firstDelay: TimeSpan.FromMilliseconds(1), interval: TimeSpan.FromMilliseconds(20));

        check.Start();

        Assert.That(await Task.WhenAny(enough.Task, Task.Delay(TimeSpan.FromSeconds(5))), Is.SameAs(enough.Task),
            "the loop keeps going after failures");
    }

    [Test]
    public void Default_period_is_thirty_minutes()
    {
        Assert.That(UpdateConstants.CheckInterval, Is.EqualTo(TimeSpan.FromMinutes(30)));
    }
}
