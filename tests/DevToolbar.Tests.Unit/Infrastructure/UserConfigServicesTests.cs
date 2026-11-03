namespace DevToolbar.Tests.Unit.Infrastructure;

using DevToolbar.Core.Models;
using DevToolbar.Infrastructure.Configuration;

[TestFixture]
public class UserConfigServicesTests
{
    private static async Task<UserConfigurationStore> StoreWithTwoProjectsAsync(TempDirectory temp)
    {
        var store = new UserConfigurationStore(temp.Paths);
        await store.UpdateAsync(c => c.Projects.AddRange([new ProjectConfig { Id = "a", Name = "A" }, new ProjectConfig { Id = "b", Name = "B" }]));
        return store;
    }

    [Test]
    public async Task ActiveProject_DefaultsToFirst_AndSwitchIsPersisted()
    {
        using var temp = new TempDirectory();
        var service = new UserConfigSettingsService(await StoreWithTwoProjectsAsync(temp));
        ProjectConfig? notified = null;
        service.OnActiveProjectChanged += p => notified = p;

        Assert.That(service.GetActiveProject()?.Id, Is.EqualTo("a"));
        await service.SetActiveProjectAsync("b");

        Assert.That(notified?.Id, Is.EqualTo("b"));
        Assert.That(new UserConfigSettingsService(new UserConfigurationStore(temp.Paths)).GetActiveProject()?.Id, Is.EqualTo("b"));
    }

    [Test]
    public async Task SetActiveProject_IgnoresUnknownId()
    {
        using var temp = new TempDirectory();
        var service = new UserConfigSettingsService(await StoreWithTwoProjectsAsync(temp));

        await service.SetActiveProjectAsync("missing");

        Assert.That(service.GetActiveProject()?.Id, Is.EqualTo("a"));
    }

    [Test]
    public async Task SaveProject_ReplacesTheProject_InTheSingleFile()
    {
        using var temp = new TempDirectory();
        var service = new UserConfigSettingsService(await StoreWithTwoProjectsAsync(temp));

        await service.SaveProjectAsync(new ProjectConfig { Id = "b", Name = "Renamed" });

        var projects = await new UserConfigSettingsService(new UserConfigurationStore(temp.Paths)).GetProjectsAsync();
        Assert.That(projects.Select(p => p.Name), Is.EqualTo(new[] { "A", "Renamed" }));
        Assert.That(service.ConfigurationLocation, Is.EqualTo(temp.Paths.ConfigFile));
    }

    [Test]
    public async Task GlobalSettings_UpdateIsPersistedInSettingsSection_AndRaisesEvent()
    {
        using var temp = new TempDirectory();
        var service = new UserConfigGlobalSettingsService(new UserConfigurationStore(temp.Paths));
        GlobalSettings? notified = null;
        service.OnSettingsChanged += s => notified = s;

        await service.LoadAsync();
        await service.UpdateAsync(s => s.ToolbarBehavior = ToolbarBehavior.DockedBottom);

        Assert.That(notified?.ToolbarBehavior, Is.EqualTo(ToolbarBehavior.DockedBottom));
        Assert.That(new UserConfigurationStore(temp.Paths).Current.Settings.ToolbarBehavior, Is.EqualTo(ToolbarBehavior.DockedBottom));
        Assert.That(File.Exists(temp.Paths.LegacyGlobalSettingsFile), Is.False);
    }
}
