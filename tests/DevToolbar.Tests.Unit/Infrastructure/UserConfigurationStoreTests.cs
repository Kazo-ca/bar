namespace DevToolbar.Tests.Unit.Infrastructure;

using System.Text.Json.Nodes;
using DevToolbar.Core.Models;
using DevToolbar.Infrastructure.Configuration;

[TestFixture]
public class UserConfigurationStoreTests
{
    private const string LegacyConfig = """
        {
          "ActiveProjectId": "beta",
          "Projects": [
            { "id": "alpha", "name": "Alpha", "path": "C:\\src\\alpha", "gitRepositories": [ { "name": "api", "path": "C:\\src\\alpha\\api" } ] },
            { "id": "beta", "name": "Beta", "path": "C:\\src\\beta" }
          ]
        }
        """;

    private const string LegacyGlobalSettings = """{ "toolbarBehavior": 1, "toolbarHeight": 64, "showActiveWindows": true }""";

    [Test]
    public void NoFiles_StartsWithDefaults_AndWritesNothing()
    {
        using var temp = new TempDirectory();

        var store = new UserConfigurationStore(temp.Paths);

        Assert.That(store.Current.Projects, Is.Empty);
        Assert.That(store.Current.SchemaVersion, Is.EqualTo(UserConfiguration.CurrentSchemaVersion));
        Assert.That(store.LoadProblem, Is.Null);
        Assert.That(File.Exists(temp.Paths.ConfigFile), Is.False);
    }

    [Test]
    public void LegacyFiles_AreMigrated_MergedIntoOneFile_AndBackedUp()
    {
        using var temp = new TempDirectory();
        temp.Write("config.json", LegacyConfig);
        temp.Write("global-settings.json", LegacyGlobalSettings);

        var store = new UserConfigurationStore(temp.Paths);

        Assert.That(store.Current.ActiveProjectId, Is.EqualTo("beta"));
        Assert.That(store.Current.Projects.Select(p => p.Id), Is.EqualTo(new[] { "alpha", "beta" }));
        Assert.That(store.Current.Projects[0].GitRepositories.Single().Path, Is.EqualTo(@"C:\src\alpha\api"));
        Assert.That(store.Current.Projects[1].GitRepositories.Single().Path, Is.EqualTo(@"C:\src\beta"));
        Assert.That(store.Current.Settings.ToolbarHeight, Is.EqualTo(64));
        Assert.That(store.Current.Settings.ShowActiveWindows, Is.True);

        var onDisk = JsonNode.Parse(File.ReadAllText(temp.Paths.ConfigFile))!;
        Assert.That(onDisk["schemaVersion"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(onDisk["settings"]!["toolbarHeight"]!.GetValue<int>(), Is.EqualTo(64));
        Assert.That(onDisk["projects"]![0]!["repositories"]![0]!["localPath"]!.GetValue<string>(), Is.EqualTo(@"C:\src\alpha\api"));

        Assert.That(File.Exists(temp.Paths.LegacyGlobalSettingsFile), Is.False);
        var backups = temp.Backups();
        Assert.That(backups.Count(b => b.StartsWith("config.v0.")), Is.EqualTo(1));
        Assert.That(backups.Count(b => b.StartsWith("global-settings.v0.")), Is.EqualTo(1));
    }

    [Test]
    public void LegacyGlobalSettingsAlone_IsMigrated()
    {
        using var temp = new TempDirectory();
        temp.Write("global-settings.json", LegacyGlobalSettings);

        var store = new UserConfigurationStore(temp.Paths);

        Assert.That(store.Current.Settings.ToolbarHeight, Is.EqualTo(64));
        Assert.That(File.Exists(temp.Paths.ConfigFile), Is.True);
        Assert.That(File.Exists(temp.Paths.LegacyGlobalSettingsFile), Is.False);
    }

    [Test]
    public void CurrentVersionFile_IsLoaded_WithoutBackup()
    {
        using var temp = new TempDirectory();
        temp.Write("config.json", """{ "schemaVersion": 1, "activeProjectId": "a", "projects": [ { "id": "a", "repositories": [ { "localPath": "/r" } ] } ] }""");

        var store = new UserConfigurationStore(temp.Paths);

        Assert.That(store.Current.Projects.Single().GitRepositories.Single().Path, Is.EqualTo("/r"));
        Assert.That(temp.Backups(), Is.Empty);
    }

    [Test]
    public async Task UpdateAsync_PersistsChanges_ThatANewStoreReadsBack()
    {
        using var temp = new TempDirectory();
        var store = new UserConfigurationStore(temp.Paths);
        var raised = 0;
        store.Changed += _ => raised++;

        await store.UpdateAsync(c =>
        {
            c.Projects.Add(new ProjectConfig { Id = "x", Name = "X" });
            c.ActiveProjectId = "x";
            c.Settings.ToolbarHeight = 72;
        });

        var reloaded = new UserConfigurationStore(temp.Paths);
        Assert.That(reloaded.Current.ActiveProjectId, Is.EqualTo("x"));
        Assert.That(reloaded.Current.Settings.ToolbarHeight, Is.EqualTo(72));
        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public async Task InvalidJson_IsBackedUp_AndNeverOverwritten()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("config.json", "{ not json");

        var store = new UserConfigurationStore(temp.Paths);
        await store.UpdateAsync(c => c.ActiveProjectId = "x");

        Assert.That(store.LoadProblem, Does.Contain("not valid JSON"));
        Assert.That(store.IsReadOnly, Is.True);
        Assert.That(File.ReadAllText(path), Is.EqualTo("{ not json"));
        Assert.That(temp.Backups().Single(), Does.StartWith("config.unreadable."));
    }

    [Test]
    public async Task NewerSchemaVersion_IsLeftUnchanged()
    {
        using var temp = new TempDirectory();
        const string newer = """{ "schemaVersion": 99, "projects": [] }""";
        var path = temp.Write("config.json", newer);

        var store = new UserConfigurationStore(temp.Paths);
        await store.UpdateAsync(c => c.ActiveProjectId = "x");

        Assert.That(store.LoadProblem, Does.Contain("schema version 99"));
        Assert.That(File.ReadAllText(path), Is.EqualTo(newer));
    }
}
