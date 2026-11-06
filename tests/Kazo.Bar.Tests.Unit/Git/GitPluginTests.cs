namespace Kazo.Bar.Tests.Unit.Git;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Plugins.Git;

[TestFixture]
public class GitPluginTests
{
    private sealed class FakeGitService : IGitService
    {
        public List<(string Command, string Path)> Calls { get; } = new();
        public Dictionary<string, GitStatus> Statuses { get; } = new();
        public GitCommandResult NextResult { get; set; } = new(true, 0, "Already up to date.", string.Empty);
        public Dictionary<string, IReadOnlyList<GitWorktree>> Worktrees { get; } = new();

        public Task<IReadOnlyList<GitWorktree>> GetWorktreesAsync(string localPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Worktrees.TryGetValue(localPath, out var list) ? list : (IReadOnlyList<GitWorktree>)Array.Empty<GitWorktree>());

        public Task<GitStatus> GetStatusAsync(string localPath, CancellationToken cancellationToken = default)
        {
            Calls.Add(("status", localPath));
            return Task.FromResult(Statuses.TryGetValue(localPath, out var status)
                ? status
                : GitStatus.Problem(GitStatusKind.FolderNotFound, "missing"));
        }

        public Task<GitCommandResult> PullAsync(string localPath, CancellationToken cancellationToken = default)
        {
            Calls.Add(("pull", localPath));
            return Task.FromResult(NextResult);
        }

        public Task<GitCommandResult> PushAsync(string localPath, CancellationToken cancellationToken = default)
        {
            Calls.Add(("push", localPath));
            return Task.FromResult(NextResult);
        }
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public List<(string Level, string Message)> Messages { get; } = new();
        public void ShowInfo(string message, string? title = null) => Messages.Add(("info", message));
        public void ShowSuccess(string message, string? title = null) => Messages.Add(("success", message));
        public void ShowError(string message, string? title = null) => Messages.Add(("error", message));
        public void ShowWarning(string message, string? title = null) => Messages.Add(("warning", message));
#pragma warning disable CS0067
        public event Action<ToastNotification>? OnNotification;
#pragma warning restore CS0067
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
        var plugin = new GitPlugin(new FakeGitService(), new SilentNotifications());

        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });

        Assert.That(plugin.SelectedRepository?.Name, Is.EqualTo("api"));
        Assert.That(plugin.Repositories, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SelectRepository_ChangesSelection_AndRaisesStateChanged()
    {
        var plugin = new GitPlugin(new FakeGitService(), new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });
        var raised = 0;
        plugin.OnStateChanged += () => raised++;

        await plugin.SelectRepositoryAsync("/src/lib");

        Assert.That(plugin.SelectedRepository?.Name, Is.EqualTo("lib"));
        Assert.That(raised, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task SelectedRepository_FallsBackToFirst_WhenSelectionRemoved()
    {
        var project = TwoRepoProject();
        var plugin = new GitPlugin(new FakeGitService(), new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = project });
        await plugin.SelectRepositoryAsync("/src/lib");

        project.GitRepositories = GitRepositoryCatalog.Remove(project.GitRepositories, "/src/lib");

        Assert.That(plugin.SelectedRepository?.Name, Is.EqualTo("api"));
    }

    [Test]
    public void IsConfigurable_WithGitSettingsPanel()
    {
        IPlugin plugin = new GitPlugin(new FakeGitService(), new SilentNotifications());

        Assert.That(plugin, Is.InstanceOf<IConfigurablePlugin>());
        Assert.That(((IConfigurablePlugin)plugin).SettingsComponentType, Is.EqualTo(typeof(GitSettingsPanel)));
    }

    [Test]
    public async Task PullAndPush_RunInsideSelectedRepository_AndRefreshStatus()
    {
        var git = new FakeGitService();
        var plugin = new GitPlugin(git, new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });
        await plugin.SelectRepositoryAsync("/src/lib");
        git.Calls.Clear();

        await plugin.PullAsync();
        await plugin.PushAsync();

        Assert.That(git.Calls, Is.EqualTo(new[] { ("pull", "/src/lib"), ("status", "/src/lib"), ("push", "/src/lib"), ("status", "/src/lib") }));
        Assert.That(plugin.SyncStatus, Is.EqualTo("✓ Push complete"));
    }

    [Test]
    public async Task PullAndPushButton_PullsThenPushes()
    {
        var git = new FakeGitService();
        var plugin = new GitPlugin(git, new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });
        git.Calls.Clear();

        await plugin.PullAndPushAsync();

        Assert.That(git.Calls.Where(c => c.Command != "status").Select(c => c.Command), Is.EqualTo(new[] { "pull", "push" }));
        Assert.That(plugin.SyncStatus, Is.EqualTo("✓ Sync complete"));
    }

    [Test]
    public async Task PullAndPushButton_DoesNotPush_WhenPullFails()
    {
        var git = new FakeGitService { NextResult = new(false, 1, string.Empty, "fatal: no network\n") };
        var plugin = new GitPlugin(git, new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });
        git.Calls.Clear();

        await plugin.PullAndPushAsync();

        Assert.That(git.Calls.Select(c => c.Command), Does.Not.Contain("push"));
        Assert.That(plugin.SyncStatus, Does.StartWith("✗ Sync failed"));
    }

    [Test]
    public async Task FailedPull_ShowsGitError()
    {
        var git = new FakeGitService { NextResult = new(false, 1, string.Empty, "fatal: could not read Username\n") };
        var notifications = new RecordingNotifications();
        var plugin = new GitPlugin(git, notifications);
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });

        await plugin.PullAsync();

        Assert.That(plugin.SyncStatus, Is.EqualTo("✗ Pull failed: fatal: could not read Username"));
        Assert.That(notifications.Messages.Single().Level, Is.EqualTo("error"));
    }

    [Test]
    public async Task PullWithConflicts_IsReportedAsWarning()
    {
        var git = new FakeGitService { NextResult = new(false, 1, "CONFLICT (content): Merge conflict in a.txt\n", "Automatic merge failed") };
        var notifications = new RecordingNotifications();
        var plugin = new GitPlugin(git, notifications);
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });

        await plugin.PullAsync();

        Assert.That(plugin.SyncStatus, Does.Contain("conflicts"));
        Assert.That(notifications.Messages.Single().Level, Is.EqualTo("warning"));
    }

    [Test]
    public async Task Status_IsReadFromTheFolder_AndOverridesConfiguredBranch()
    {
        var git = new FakeGitService();
        git.Statuses["/src/api"] = new GitStatus { Kind = GitStatusKind.Repository, Branch = "feature/x", ChangedFiles = 3 };
        var plugin = new GitPlugin(git, new SilentNotifications());

        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });

        Assert.That(plugin.CurrentBranch, Is.EqualTo("feature/x"));
        Assert.That(plugin.IsDirty, Is.True);
        Assert.That(plugin.Status?.ChangedFiles, Is.EqualTo(3));
    }

    [Test]
    public async Task UnreadableFolder_KeepsConfiguredBranch_AndReportsProblem()
    {
        var plugin = new GitPlugin(new FakeGitService(), new SilentNotifications());
        var project = TwoRepoProject();
        project.DefaultBranch = "develop";

        await plugin.OnProjectChangedAsync(new PluginContext { Project = project });

        Assert.That(plugin.CurrentBranch, Is.EqualTo("develop"));
        Assert.That(plugin.Status?.Kind, Is.EqualTo(GitStatusKind.FolderNotFound));
        Assert.That(plugin.IsDirty, Is.False);
    }

    [Test]
    public async Task CurrentBranch_FollowsSelectedRepository()
    {
        var project = TwoRepoProject();
        project.DefaultBranch = "develop";
        project.GitRepositories = [project.GitRepositories[0], project.GitRepositories[1] with { DefaultBranch = "main" }];
        var plugin = new GitPlugin(new FakeGitService(), new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = project });

        Assert.That(plugin.CurrentBranch, Is.EqualTo("develop"));
        await plugin.SelectRepositoryAsync("/src/lib");
        Assert.That(plugin.CurrentBranch, Is.EqualTo("main"));
    }

    private static FakeGitService WithApiWorktrees()
    {
        var git = new FakeGitService();
        git.Worktrees["/src/api"] =
        [
            new GitWorktree { Path = "/src/api", Branch = "main", IsMain = true },
            new GitWorktree { Path = "/src/api-hotfix", Branch = "hotfix/x" },
        ];
        git.Statuses["/src/api"] = new GitStatus { Kind = GitStatusKind.Repository, Branch = "main" };
        git.Statuses["/src/api-hotfix"] = new GitStatus { Kind = GitStatusKind.Repository, Branch = "hotfix/x", ChangedFiles = 1 };
        return git;
    }

    [Test]
    public async Task Worktrees_AreLoadedWithTheProject_AndTheMainOneIsCurrent()
    {
        var plugin = new GitPlugin(WithApiWorktrees(), new SilentNotifications());

        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });

        Assert.That(plugin.HasWorktrees(plugin.Repositories[0]), Is.True);
        Assert.That(plugin.HasWorktrees(plugin.Repositories[1]), Is.False);
        Assert.That(plugin.CurrentWorktree?.Path, Is.EqualTo("/src/api"));
        Assert.That(plugin.ActivePath, Is.EqualTo("/src/api"));
    }

    [Test]
    public async Task NoCurrentWorktree_WhenTheRepositoryHasOnlyItsMainTree()
    {
        var plugin = new GitPlugin(WithApiWorktrees(), new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });

        await plugin.SelectRepositoryAsync("/src/lib");

        Assert.That(plugin.CurrentWorktree, Is.Null);
    }

    [Test]
    public async Task SelectWorktree_ReadsStatusAndRunsPullInTheWorktreeFolder()
    {
        var git = WithApiWorktrees();
        var notifications = new RecordingNotifications();
        var plugin = new GitPlugin(git, notifications);
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });

        await plugin.SelectWorktreeAsync("/src/api", "/src/api-hotfix");
        git.Calls.Clear();
        await plugin.PullAsync();

        Assert.That(plugin.CurrentWorktree?.Name, Is.EqualTo("api-hotfix"));
        Assert.That(plugin.CurrentBranch, Is.EqualTo("hotfix/x"));
        Assert.That(plugin.IsDirty, Is.True);
        Assert.That(git.Calls[0], Is.EqualTo(("pull", "/src/api-hotfix")));
        Assert.That(notifications.Messages.Last().Message, Does.Contain("api (api-hotfix)"));
    }

    [Test]
    public async Task SelectingTheMainWorktree_OrAnotherRepository_LeavesTheLinkedWorktree()
    {
        var plugin = new GitPlugin(WithApiWorktrees(), new SilentNotifications());
        await plugin.OnProjectChangedAsync(new PluginContext { Project = TwoRepoProject() });
        await plugin.SelectWorktreeAsync("/src/api", "/src/api-hotfix");

        await plugin.SelectWorktreeAsync("/src/api", "/src/api/");
        Assert.That(plugin.ActivePath, Is.EqualTo("/src/api"));

        await plugin.SelectWorktreeAsync("/src/api", "/src/api-hotfix");
        await plugin.SelectRepositoryAsync("/src/lib");
        Assert.That(plugin.ActivePath, Is.EqualTo("/src/lib"));
    }

    [Test]
    public void GitModule_IsFixed_AndFillsTheProjectPill()
    {
        IPlugin plugin = new GitPlugin(new FakeGitService(), new SilentNotifications());

        Assert.That(plugin.Layout.Resizable, Is.False, "names live in the project pill, which is the resizable one");
        Assert.That(plugin, Is.InstanceOf<IProjectPillContent>());
        Assert.That(((IProjectPillContent)plugin).RenderPillInfo(), Is.Not.Null);
    }
}
