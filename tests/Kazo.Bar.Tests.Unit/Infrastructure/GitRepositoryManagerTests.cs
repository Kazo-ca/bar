namespace Kazo.Bar.Tests.Unit.Infrastructure;

using System.Diagnostics;
using Kazo.Bar.Core.Configuration;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Git;

/// <summary>Repository management (US5.7, US5.8) against real repositories made with the git CLI.</summary>
[TestFixture]
public class GitRepositoryManagerTests
{
    private TempDirectory _temp = null!;
    private string _repo = null!;
    private GitService _git = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempDirectory();
        _repo = Path.Combine(_temp.Root, "repo");
        Directory.CreateDirectory(_repo);
        Git(_repo, "init", "--initial-branch=main");
        Git(_repo, "config", "user.name", "Test");
        Git(_repo, "config", "user.email", "test@example.com");
        Git(_repo, "config", "commit.gpgsign", "false");
        File.WriteAllText(Path.Combine(_repo, "readme.md"), "one\ntwo\nthree\n");
        Git(_repo, "add", ".");
        Git(_repo, "commit", "-m", "initial");
        _git = new GitService();
    }

    [TearDown]
    public void TearDown() => _temp.Dispose();

    [Test]
    public async Task Changes_ListStagedUnstagedAndUntracked_WithLineCounts()
    {
        File.WriteAllText(Path.Combine(_repo, "readme.md"), "one\n2\nthree\nfour\n");
        File.WriteAllText(Path.Combine(_repo, "staged.txt"), "a\nb\n");
        Git(_repo, "add", "staged.txt");
        Directory.CreateDirectory(Path.Combine(_repo, "src"));
        File.WriteAllText(Path.Combine(_repo, "src", "new file.cs"), "x\ny\nz");

        var changes = await _git.GetChangesAsync(_repo);

        Assert.That(changes.Error, Is.Null);
        Assert.That(changes.Staged.Single().Path, Is.EqualTo("staged.txt"));
        Assert.That(changes.Staged.Single().LinesAdded, Is.EqualTo(2));
        var readme = changes.Unstaged.Single(c => c.Path == "readme.md");
        Assert.That((readme.Kind, readme.LinesAdded, readme.LinesDeleted), Is.EqualTo((GitChangeKind.Modified, 2, 1)));
        var untracked = changes.Unstaged.Single(c => c.Path == "src/new file.cs");
        Assert.That((untracked.Kind, untracked.LinesAdded), Is.EqualTo((GitChangeKind.Untracked, 3)));

        var status = await _git.GetStatusAsync(_repo);
        Assert.That(status.LinesAdded, Is.EqualTo(2 + 2 + 3));
        Assert.That(status.LinesDeleted, Is.EqualTo(1));
        Assert.That(status.ChangedFiles, Is.EqualTo(3));
    }

    [Test]
    public async Task FileDiff_OfWorkingTree_Index_AndUntrackedFile()
    {
        File.WriteAllText(Path.Combine(_repo, "readme.md"), "one\n2\nthree\n");
        File.WriteAllText(Path.Combine(_repo, "new.txt"), "hello\n");
        var changes = await _git.GetChangesAsync(_repo);

        var modified = await _git.GetFileDiffAsync(_repo, changes.Unstaged.Single(c => c.Path == "readme.md"));
        var added = await _git.GetFileDiffAsync(_repo, changes.Unstaged.Single(c => c.Path == "new.txt"));

        Assert.That(modified.Error, Is.Null);
        Assert.That((modified.LinesAdded, modified.LinesDeleted), Is.EqualTo((1, 1)));
        Assert.That(modified.Hunks[0].Lines.Single(l => l.Kind == GitDiffLineKind.Added).Text, Is.EqualTo("2"));
        Assert.That(added.Hunks.Single().Lines.Single().Text, Is.EqualTo("hello"));

        Assert.That((await _git.StageAsync(_repo, ["readme.md"])).Success, Is.True);
        var staged = (await _git.GetChangesAsync(_repo)).Staged.Single();
        var stagedDiff = await _git.GetFileDiffAsync(_repo, staged);
        Assert.That(stagedDiff.LinesAdded, Is.EqualTo(1));
    }

    // Brackets are pathspec glob characters: the file must still be matched literally.
    // No '*' or '?': Windows forbids them in file names.
    private const string WeirdName = "[weird] name.txt";

    [Test]
    public async Task StageUnstageAndDiscard_ChangeTheWorkingCopy()
    {
        File.WriteAllText(Path.Combine(_repo, "readme.md"), "changed\n");
        File.WriteAllText(Path.Combine(_repo, WeirdName), "x\n");

        var stage = await _git.StageAsync(_repo, ["readme.md", WeirdName]);
        Assert.That(stage.Success, Is.True, stage.Error);
        Assert.That((await _git.GetChangesAsync(_repo)).Staged, Has.Count.EqualTo(2));

        var unstage = await _git.UnstageAsync(_repo, ["readme.md", WeirdName]);
        Assert.That(unstage.Success, Is.True, unstage.Error);
        var changes = await _git.GetChangesAsync(_repo);
        Assert.That(changes.Staged, Is.Empty);

        var discard = await _git.DiscardAsync(_repo, changes.Unstaged);
        Assert.That(discard.Success, Is.True, discard.Error);
        Assert.That((await _git.GetChangesAsync(_repo)).HasChanges, Is.False);
        Assert.That(File.ReadAllText(Path.Combine(_repo, "readme.md")), Is.EqualTo("one\ntwo\nthree\n"));
    }

    [Test]
    public async Task Commit_RecordsStagedChanges_OrEverythingWithStageAll()
    {
        File.WriteAllText(Path.Combine(_repo, "a.txt"), "a\n");

        var empty = await _git.CommitAsync(_repo, "   ");
        Assert.That(empty.Success, Is.False);

        var commit = await _git.CommitAsync(_repo, "Add a.txt\n\nWith a body.", stageAll: true);
        Assert.That(commit.Success, Is.True, commit.Error);
        Assert.That((await _git.GetChangesAsync(_repo)).HasChanges, Is.False);
        Assert.That(Output(_repo, "log", "-1", "--format=%B").Trim(), Is.EqualTo("Add a.txt\n\nWith a body."));

        var amend = await _git.CommitAsync(_repo, "Add a.txt (amended)", amend: true);
        Assert.That(amend.Success, Is.True, amend.Error);
        Assert.That(Output(_repo, "rev-list", "--count", "HEAD").Trim(), Is.EqualTo("2"));
    }

    [Test]
    public async Task Branches_CreateCheckoutAndDelete()
    {
        var create = await _git.CreateBranchAsync(_repo, "feature/login", null, checkout: true);
        Assert.That(create.Success, Is.True, create.Error);
        Assert.That((await _git.GetStatusAsync(_repo)).Branch, Is.EqualTo("feature/login"));

        var invalid = await _git.CreateBranchAsync(_repo, "bad name", null, checkout: false);
        Assert.That(invalid.Success, Is.False);

        var branches = await _git.GetBranchesAsync(_repo);
        Assert.That(branches.Select(b => b.Name), Is.EquivalentTo(new[] { "feature/login", "main" }));
        Assert.That(branches[0].IsCurrent, Is.True);
        Assert.That(branches[0].TipMessage, Is.EqualTo("initial"));

        var checkout = await _git.CheckoutAsync(_repo, branches.Single(b => b.Name == "main"));
        Assert.That(checkout.Success, Is.True, checkout.Error);
        var delete = await _git.DeleteBranchAsync(_repo, "feature/login");
        Assert.That(delete.Success, Is.True, delete.Error);
        Assert.That((await _git.GetBranchesAsync(_repo)).Select(b => b.Name), Is.EqualTo(new[] { "main" }));
    }

    [Test]
    public async Task RemoteBranch_IsCheckedOutAsATrackingLocalBranch()
    {
        var remote = Path.Combine(_temp.Root, "remote.git");
        Git(_temp.Root, "init", "--bare", "--initial-branch=main", remote);
        Git(_repo, "remote", "add", "origin", remote);
        Git(_repo, "push", "origin", "main");
        Git(_repo, "push", "origin", "main:release/1.0");
        Assert.That((await _git.FetchAsync(_repo)).Success, Is.True);

        var remoteBranch = (await _git.GetBranchesAsync(_repo)).Single(b => b.Name == "origin/release/1.0");
        Assert.That(remoteBranch.IsRemote, Is.True);
        var checkout = await _git.CheckoutAsync(_repo, remoteBranch);

        Assert.That(checkout.Success, Is.True, checkout.Error);
        var current = (await _git.GetBranchesAsync(_repo)).Single(b => b.IsCurrent);
        Assert.That((current.Name, current.Upstream), Is.EqualTo(("release/1.0", "origin/release/1.0")));
    }

    [Test]
    public async Task Worktrees_AddFromSourceBranch_MoveAndRemove()
    {
        var folder = Path.Combine(_temp.Root, "repo.worktrees", "feature-x");

        var add = await _git.AddWorktreeAsync(_repo, folder, "main", "feature/x");
        Assert.That(add.Success, Is.True, add.Error);
        var worktrees = await _git.GetWorktreesAsync(_repo);
        Assert.That(worktrees.Select(w => w.Branch), Is.EqualTo(new[] { "main", "feature/x" }));
        Assert.That((await _git.GetBranchesAsync(_repo)).Single(b => b.Name == "feature/x").CheckedOutIn, Is.Not.Null);

        var moved = Path.Combine(_temp.Root, "elsewhere", "fx");
        var move = await _git.MoveWorktreeAsync(_repo, folder, moved);
        Assert.That(move.Success, Is.True, move.Error);
        Assert.That(RepositoryPaths.SamePath((await _git.GetWorktreesAsync(_repo))[1].Path, moved), Is.True);

        File.WriteAllText(Path.Combine(moved, "dirty.txt"), "x");
        var refused = await _git.RemoveWorktreeAsync(_repo, moved);
        Assert.That(refused.Success, Is.False);
        var forced = await _git.RemoveWorktreeAsync(_repo, moved, force: true);
        Assert.That(forced.Success, Is.True, forced.Error);
        Assert.That(Directory.Exists(moved), Is.False);
        Assert.That(await _git.GetWorktreesAsync(_repo), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Worktree_OfExistingBranch_AndIntoANonEmptyFolder()
    {
        Git(_repo, "branch", "hotfix");
        var occupied = Path.Combine(_temp.Root, "occupied");
        Directory.CreateDirectory(occupied);
        File.WriteAllText(Path.Combine(occupied, "file.txt"), "x");

        var refused = await _git.AddWorktreeAsync(_repo, occupied, "hotfix", null);
        var add = await _git.AddWorktreeAsync(_repo, Path.Combine(_temp.Root, "hotfix"), "hotfix", null);

        Assert.That(refused.Success, Is.False);
        Assert.That(add.Success, Is.True, add.Error);
        Assert.That((await _git.GetStatusAsync(Path.Combine(_temp.Root, "hotfix"))).Branch, Is.EqualTo("hotfix"));
    }

    [Test]
    public async Task Changes_OutsideARepository_ReportAnError()
    {
        var changes = await _git.GetChangesAsync(_temp.Root);

        Assert.That(changes.Error, Is.Not.Null);
        Assert.That(changes.HasChanges, Is.False);
    }

    [Test]
    public async Task Unstage_BeforeTheFirstCommit_WorksForAFileEditedAfterStaging()
    {
        var fresh = Path.Combine(_temp.Root, "fresh");
        Directory.CreateDirectory(fresh);
        Git(fresh, "init", "--initial-branch=main");
        File.WriteAllText(Path.Combine(fresh, "f.txt"), "one\n");
        Git(fresh, "add", "f.txt");
        File.WriteAllText(Path.Combine(fresh, "f.txt"), "two\n");

        var result = await _git.UnstageAsync(fresh, ["f.txt"]);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That((await _git.GetChangesAsync(fresh)).Staged, Is.Empty);
    }

    [Test]
    public async Task StagedRename_IsListedWithItsOldPath_AndUnstagedWithIt()
    {
        Git(_repo, "mv", "readme.md", "README.txt");
        var rename = (await _git.GetChangesAsync(_repo)).Staged.Single();
        Assert.That((rename.Kind, rename.Path, rename.OldPath), Is.EqualTo((GitChangeKind.Renamed, "README.txt", "readme.md")));

        var result = await _git.UnstageAsync(_repo, [rename.Path, rename.OldPath!]);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That((await _git.GetChangesAsync(_repo)).Staged, Is.Empty);
    }

    [Test]
    public async Task OptionLikeNames_AreRefused()
    {
        Assert.That((await _git.CreateBranchAsync(_repo, "ok", "--orphan", checkout: false)).Success, Is.False);
        Assert.That((await _git.RemoveWorktreeAsync(_repo, "--force")).Success, Is.False);
        Assert.That((await _git.DeleteBranchAsync(_repo, "-D")).Success, Is.False);
    }

    private static void Git(string directory, params string[] arguments)
    {
        var (code, _, error) = Run(directory, arguments);
        Assert.That(code, Is.EqualTo(0), error);
    }

    private static string Output(string directory, params string[] arguments) => Run(directory, arguments).Output;

    private static (int Code, string Output, string Error) Run(string directory, string[] arguments)
    {
        var info = new ProcessStartInfo("git") { RedirectStandardError = true, RedirectStandardOutput = true };
        info.ArgumentList.Add("-C");
        info.ArgumentList.Add(directory);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }
}
