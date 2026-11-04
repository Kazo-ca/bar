namespace Kazo.Bar.Tests.Unit.Infrastructure;

using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Git;
using LibGit2Sharp;

[TestFixture]
public class GitServiceTests
{
    private static readonly Signature Author = new("Test", "test@example.com", DateTimeOffset.Now);

    private static string InitRepository(string path, string branch = "main")
    {
        Directory.CreateDirectory(path);
        Repository.Init(path);
        using var repository = new Repository(path);
        repository.Refs.UpdateTarget("HEAD", $"refs/heads/{branch}");
        File.WriteAllText(Path.Combine(path, "readme.md"), "hello");
        Commands.Stage(repository, "readme.md");
        repository.Commit("initial", Author, Author);
        return path;
    }

    [Test]
    public async Task MissingFolder_IsReported()
    {
        var status = await new GitService().GetStatusAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.That(status.Kind, Is.EqualTo(GitStatusKind.FolderNotFound));
    }

    [Test]
    public async Task FolderOutsideRepository_IsReported()
    {
        using var temp = new TempDirectory();

        var status = await new GitService().GetStatusAsync(temp.Root);

        Assert.That(status.Kind, Is.EqualTo(GitStatusKind.NotARepository));
    }

    [Test]
    public async Task CleanRepository_ReportsCheckedOutBranch()
    {
        using var temp = new TempDirectory();
        var path = InitRepository(Path.Combine(temp.Root, "repo"), "develop");

        var status = await new GitService().GetStatusAsync(path);

        Assert.That(status.Kind, Is.EqualTo(GitStatusKind.Repository));
        Assert.That(status.Branch, Is.EqualTo("develop"));
        Assert.That(status.IsDirty, Is.False);
        Assert.That(status.Ahead, Is.Null);
    }

    [Test]
    public async Task ModifiedAndUntrackedFiles_MakeTheRepositoryDirty_FromASubfolderToo()
    {
        using var temp = new TempDirectory();
        var path = InitRepository(Path.Combine(temp.Root, "repo"));
        File.WriteAllText(Path.Combine(path, "readme.md"), "changed");
        File.WriteAllText(Path.Combine(path, "new.txt"), "new");
        Directory.CreateDirectory(Path.Combine(path, "src"));

        var status = await new GitService().GetStatusAsync(Path.Combine(path, "src"));

        Assert.That(status.IsDirty, Is.True);
        Assert.That(status.ChangedFiles, Is.EqualTo(2));
    }

    [Test]
    public async Task DetachedHead_ShowsShortCommitId()
    {
        using var temp = new TempDirectory();
        var path = InitRepository(Path.Combine(temp.Root, "repo"));
        using (var repository = new Repository(path))
            Commands.Checkout(repository, repository.Head.Tip);

        var status = await new GitService().GetStatusAsync(path);

        Assert.That(status.IsDetached, Is.True);
        Assert.That(status.Branch, Has.Length.EqualTo(7));
    }

    [Test]
    public async Task PushThenPull_UseTheGitCli_AndReportAheadCounts()
    {
        using var temp = new TempDirectory();
        var remote = Path.Combine(temp.Root, "remote.git");
        Repository.Init(remote, isBare: true);
        var path = InitRepository(Path.Combine(temp.Root, "repo"));
        using (var repository = new Repository(path))
        {
            repository.Network.Remotes.Add("origin", remote);
            repository.Branches.Update(repository.Head, b => b.Remote = "origin", b => b.UpstreamBranch = "refs/heads/main");
        }
        var git = new GitService();

        var push = await git.PushAsync(path);
        Assert.That(push.Success, Is.True, push.Error);

        using (var repository = new Repository(path))
        {
            File.WriteAllText(Path.Combine(path, "two.txt"), "2");
            Commands.Stage(repository, "two.txt");
            repository.Commit("second", Author, Author);
        }
        var status = await git.GetStatusAsync(path);
        Assert.That(status.Ahead, Is.EqualTo(1));
        Assert.That(status.Behind, Is.EqualTo(0));

        var pull = await git.PullAsync(path);
        Assert.That(pull.Success, Is.True, pull.Error);
    }

    [Test]
    public async Task FailedCommand_ReturnsGitError()
    {
        using var temp = new TempDirectory();
        var path = InitRepository(Path.Combine(temp.Root, "repo"));

        var result = await new GitService().PushAsync(path);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Summary, Is.Not.Empty);
    }

    [Test]
    public async Task MissingGitExecutable_IsReported()
    {
        using var temp = new TempDirectory();
        var git = new GitService(null, "git-does-not-exist-" + Guid.NewGuid().ToString("N"), TimeSpan.FromSeconds(5));

        var result = await git.PullAsync(temp.Root);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("Could not run git"));
    }

    [Test]
    public async Task Worktrees_ListsMainThenLinked_FromAnyOfThem()
    {
        using var temp = new TempDirectory();
        var path = InitRepository(Path.Combine(temp.Root, "repo"));
        var linked = Path.Combine(temp.Root, "repo-hotfix");
        RunGit(path, "worktree", "add", "-b", "hotfix", linked);

        var fromMain = await new GitService().GetWorktreesAsync(path);
        var fromLinked = await new GitService().GetWorktreesAsync(linked);

        Assert.That(fromMain, Has.Count.EqualTo(2));
        Assert.That(fromMain[0].IsMain, Is.True);
        Assert.That(fromMain[0].Branch, Is.EqualTo("main"));
        Assert.That(Kazo.Bar.Core.Configuration.RepositoryPaths.SamePath(fromMain[1].Path, linked), Is.True);
        Assert.That(fromMain[1].Branch, Is.EqualTo("hotfix"));
        Assert.That(fromLinked.Select(w => w.Branch), Is.EqualTo(fromMain.Select(w => w.Branch)));
    }

    [Test]
    public async Task Worktrees_OfAFolderOutsideRepository_IsEmpty()
    {
        using var temp = new TempDirectory();

        Assert.That(await new GitService().GetWorktreesAsync(temp.Root), Is.Empty);
    }

    private static void RunGit(string directory, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { RedirectStandardError = true, RedirectStandardOutput = true };
        info.ArgumentList.Add("-C");
        info.ArgumentList.Add(directory);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        process.WaitForExit();
        Assert.That(process.ExitCode, Is.EqualTo(0), process.StandardError.ReadToEnd());
    }
}
