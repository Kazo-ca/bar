namespace Kazo.Bar.Tests.Unit.TimeTracking;

using System.Text.Json;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Storage;

[TestFixture]
public class TimeTrackingConfigTests
{
    [Test]
    public void ConfigWithoutTimeTracking_GetsTheDefaults()
    {
        var config = JsonSerializer.Deserialize<UserConfiguration>(
            """{ "schemaVersion": 1, "settings": { "toolbarHeight": 56 }, "projects": [ { "id": "a", "repositories": [ { "name": "api" } ] } ] }""",
            VersionedJson.SerializerOptions)!;

        Assert.That(config.Settings.TimeTracking.IdleMinutes, Is.EqualTo(15));
        Assert.That(config.Settings.TimeTracking.Lunch.Start, Is.EqualTo(new TimeOnly(12, 0)));
        Assert.That(config.Settings.TimeTracking.DailyMeeting.Enabled, Is.False);
        Assert.That(config.Projects[0].GitRepositories[0].TimeTracking, Is.Null);
    }

    [Test]
    public void RulesAndRepositoryOverrides_RoundTrip()
    {
        var config = new UserConfiguration();
        config.Settings.TimeTracking.DailyMeeting.Enabled = true;
        config.Settings.TimeTracking.IdleMinutes = 20;
        config.Projects.Add(new ProjectConfig
        {
            Id = "a",
            GitRepositories = [new GitRepository { Name = "api", TimeTracking = new TimeTrackingOverrides { IdleMinutes = 5 } }],
        });

        var json = JsonSerializer.Serialize(config, VersionedJson.SerializerOptions);
        var back = JsonSerializer.Deserialize<UserConfiguration>(json, VersionedJson.SerializerOptions)!;

        Assert.That(back.Settings.TimeTracking.IdleMinutes, Is.EqualTo(20));
        Assert.That(back.Settings.TimeTracking.DailyMeeting.Enabled, Is.True);
        Assert.That(back.Settings.TimeTracking.DailyMeeting.Days, Has.Count.EqualTo(5));
        Assert.That(back.Projects[0].GitRepositories[0].TimeTracking!.IdleMinutes, Is.EqualTo(5));
        Assert.That(back.Projects[0].GitRepositories[0].TimeTracking!.Lunch, Is.Null);
    }
}
