namespace DevToolbar.Tests.Unit.Git;

using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using DevToolbar.Plugins.Git;

[TestFixture]
public class GitPluginTests
{
    private sealed class RecordingProcessService : IProcessService
    {
        public List<(string Path, string Args)> Calls { get; } = new();
        public Task<int> StartProcessAsync(string path, string arguments = "")
        {
            Calls.Add((path, arguments));
            return Task.FromResult(0);
        }
        public bool FocusWindowByTitle(string titleRegex) => false;
    }

    private sealed class SilentNotifications : INotificationService
    {
        public void ShowInfo(string message, string? title = null) { }
        public void ShowSuccess(string message, string? title = null) { }
        public void ShowError(string message, string? title = null) { }
        public void ShowWarning(string message, string? title = null) { }
#pragma warning disable CS0067
        public event Action<ToastNotification>? OnNotification;
#pragma warning restore CS0067
    }

    private static ProjectConfig TwoRepoProject() => new()
    {
        Id = "p",
        Path = "/p",
        GitRepositories =
        [
            new GitRepository { Name = "api", Path = "/src/api" },
            new GitRepository { Name = "lib", Path = "/src/lib" },
        ],
    };

    [Test]
    public async Task OnProjectChanged_SelectsFirstRepository()
    {
        var plugin = new GitPlugin(new RecordingProcessService(), new SilentNotifications());

        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });

        Assert.That(plugin.SelectedRepository?.Name, Is.EqualTo("api"));
        Assert.That(plugin.Repositories, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SelectRepository_ChangesSelection_AndRaisesStateChanged()
    {
        var plugin = new GitPlugin(new RecordingProcessService(), new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });
        var raised = 0;
        plugin.OnStateChanged += () => raised++;

        plugin.SelectRepository("/src/lib");

        Assert.That(plugin.SelectedRepository?.Name, Is.EqualTo("lib"));
        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public async Task SelectedRepository_FallsBackToFirst_WhenSelectionRemoved()
    {
        var project = TwoRepoProject();
        var plugin = new GitPlugin(new RecordingProcessService(), new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = project });
        plugin.SelectRepository("/src/lib");

        project.GitRepositories = GitRepositoryCatalog.Remove(project.GitRepositories, "/src/lib");

        Assert.That(plugin.SelectedRepository?.Name, Is.EqualTo("api"));
    }

    [Test]
    public void IsConfigurable_WithGitSettingsPanel()
    {
        IPlugin plugin = new GitPlugin(new RecordingProcessService(), new SilentNotifications());

        Assert.That(plugin, Is.InstanceOf<IConfigurablePlugin>());
        Assert.That(((IConfigurablePlugin)plugin).SettingsComponentType, Is.EqualTo(typeof(GitSettingsPanel)));
    }

    [Test]
    public async Task PullAndPush_RunGitInsideSelectedRepository()
    {
        var processes = new RecordingProcessService();
        var plugin = new GitPlugin(processes, new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });
        plugin.SelectRepository("/src/lib");

        await plugin.PullAsync();
        await plugin.PushAsync();

        Assert.That(processes.Calls, Is.EqualTo(new[] { ("git", "-C \"/src/lib\" pull"), ("git", "-C \"/src/lib\" push") }));
        Assert.That(plugin.SyncStatus, Is.EqualTo("✓ Push complete"));
    }

    [Test]
    public async Task CurrentBranch_FollowsSelectedRepository()
    {
        var project = TwoRepoProject();
        project.DefaultBranch = "develop";
        project.GitRepositories = [project.GitRepositories[0], project.GitRepositories[1] with { DefaultBranch = "main" }];
        var plugin = new GitPlugin(new RecordingProcessService(), new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = project });

        Assert.That(plugin.CurrentBranch, Is.EqualTo("develop"));
        plugin.SelectRepository("/src/lib");
        Assert.That(plugin.CurrentBranch, Is.EqualTo("main"));
    }
}
