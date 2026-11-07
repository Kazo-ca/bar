namespace Kazo.Bar.Tests.Unit.Git;

using Kazo.Bar.Core.Git;
using Kazo.Bar.Core.Models;

[TestFixture]
public class GitBranchNamesTests
{
    [TestCase("feature/login")]
    [TestCase("hotfix-1.2")]
    [TestCase("user/benoit/US5.7")]
    public void ValidNames_AreAccepted(string name) => Assert.That(GitBranchNames.Validate(name), Is.Null);

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("has space")]
    [TestCase("-option")]
    [TestCase("a..b")]
    [TestCase("ends.")]
    [TestCase("ends/")]
    [TestCase("a//b")]
    [TestCase("x.lock")]
    [TestCase("a/.hidden")]
    [TestCase("what?")]
    [TestCase("a@{b")]
    [TestCase("HEAD")]
    public void InvalidNames_AreRefused(string name) => Assert.That(GitBranchNames.Validate(name), Is.Not.Null);

    [Test]
    public void RemoteBranch_LocalName_DropsTheRemote()
    {
        Assert.That(new GitBranch { Name = "origin/feature/x", IsRemote = true }.LocalName, Is.EqualTo("feature/x"));
        Assert.That(new GitBranch { Name = "feature/x" }.LocalName, Is.EqualTo("feature/x"));
    }
}

[TestFixture]
public class GitWorktreeLocationsTests
{
    [Test]
    public void DefaultFolder_IsNextToTheRepository()
    {
        var repository = new GitRepository { Path = @"C:\src\api\" };

        Assert.That(GitWorktreeLocations.RootFor(repository), Is.EqualTo(@"C:\src\api.worktrees"));
        Assert.That(GitWorktreeLocations.SuggestPath(repository, "feature/login"), Is.EqualTo(@"C:\src\api.worktrees\feature-login"));
    }

    [Test]
    public void ConfiguredFolder_IsUsed()
    {
        var repository = new GitRepository { Path = "/home/me/api", WorktreesFolder = "/work/trees/" };

        Assert.That(GitWorktreeLocations.SuggestPath(repository, "hotfix"), Is.EqualTo("/work/trees/hotfix"));
    }

    [TestCase("feature/a:b", "feature-a-b")]
    [TestCase("...", "worktree")]
    public void FolderName_IsSafe(string branch, string expected) =>
        Assert.That(GitWorktreeLocations.FolderName(branch), Is.EqualTo(expected));

    [Test]
    public void Folder_MustBeAFullPath()
    {
        Assert.That(GitWorktreeLocations.ValidateFolder("relative/path"), Is.Not.Null);
        Assert.That(GitWorktreeLocations.ValidateFolder(""), Is.Not.Null);
        Assert.That(GitWorktreeLocations.ValidateFolder("/abs/path"), Is.Null);
    }
}
