namespace DevToolbar.Tests.Unit.Git;

using DevToolbar.Core.Models;
using DevToolbar.Plugins.Git;

[TestFixture]
public class GitRepositoryCatalogTests
{
    private static GitRepository Repo(string path, string name = "", string remote = "", string branch = "") =>
        new() { Path = path, Name = name, RemoteUrl = remote, DefaultBranch = branch };

    [Test]
    public void Resolve_FallsBackToProjectFolder_WhenNoRepositoryConfigured()
    {
        var project = new ProjectConfig { Name = "Api", Path = @"C:\src\api", DefaultBranch = "develop", RepositoryUrl = "https://x/api" };

        var result = GitRepositoryCatalog.Resolve(project);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Path, Is.EqualTo(@"C:\src\api"));
        Assert.That(result[0].Name, Is.EqualTo("Api"));
        Assert.That(result[0].RemoteUrl, Is.EqualTo("https://x/api"));
    }

    [Test]
    public void Resolve_ReturnsNothing_WhenNoRepositoryAndNoPath()
    {
        Assert.That(GitRepositoryCatalog.Resolve(new ProjectConfig()), Is.Empty);
    }

    [Test]
    public void Resolve_ReturnsConfiguredRepositories_InOrder()
    {
        var project = new ProjectConfig { Path = "/p", GitRepositories = [Repo("/a", "a"), Repo("/b", "b")] };

        Assert.That(GitRepositoryCatalog.Resolve(project).Select(r => r.Name), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Normalize_TrimsFields_AndDefaultsNameToFolder()
    {
        var result = GitRepositoryCatalog.Normalize(Repo(@"  C:\src\shared-lib\  ", " ", " https://h/r ", " main "));

        Assert.That(result.Path, Is.EqualTo(@"C:\src\shared-lib\"));
        Assert.That(result.Name, Is.EqualTo("shared-lib"));
        Assert.That(result.RemoteUrl, Is.EqualTo("https://h/r"));
        Assert.That(result.DefaultBranch, Is.EqualTo("main"));
    }

    [TestCase("", "The local folder is required.")]
    [TestCase("C:\\a\"b", "The local folder contains invalid characters.")]
    public void Validate_RejectsBadPath(string path, string expected)
    {
        Assert.That(GitRepositoryCatalog.Validate([], Repo(path, "n")), Is.EqualTo(expected));
    }

    [Test]
    public void Validate_RejectsDuplicatePath_IgnoringCaseAndTrailingSeparator()
    {
        var existing = new[] { Repo(@"C:\src\api", "api") };

        var error = GitRepositoryCatalog.Validate(existing, Repo(@"c:\SRC\api\", "other"));

        Assert.That(error, Is.EqualTo("This folder is already in the list."));
    }

    [Test]
    public void Validate_RejectsDuplicateName()
    {
        var error = GitRepositoryCatalog.Validate([Repo("/a", "API")], Repo("/b", "api"));

        Assert.That(error, Is.EqualTo("Another repository already uses this name."));
    }

    [TestCase("https://github.com/org/repo.git", true)]
    [TestCase("ssh://git@host/org/repo.git", true)]
    [TestCase("git@github.com:org/repo.git", true)]
    [TestCase("ftp://host/repo", false)]
    [TestCase("not a url", false)]
    public void Validate_ChecksRemoteUrlFormat(string remote, bool isValid)
    {
        var error = GitRepositoryCatalog.Validate([], Repo("/a", "a", remote));

        Assert.That(error == null, Is.EqualTo(isValid), error);
    }

    [Test]
    public void Validate_RejectsBranchWithSpaces()
    {
        Assert.That(GitRepositoryCatalog.Validate([], Repo("/a", "a", branch: "my branch")),
            Is.EqualTo("The branch name cannot contain spaces."));
    }

    [Test]
    public void Add_ReturnsNewList_WithoutMutatingInput()
    {
        var existing = new List<GitRepository> { Repo("/a", "a") };

        var result = GitRepositoryCatalog.Add(existing, Repo(" /b ", ""));

        Assert.That(existing, Has.Count.EqualTo(1));
        Assert.That(result.Select(r => r.Path), Is.EqualTo(new[] { "/a", "/b" }));
        Assert.That(result[1].Name, Is.EqualTo("b"));
    }

    [Test]
    public void Add_Throws_WhenInvalid()
    {
        Assert.Throws<ArgumentException>(() => GitRepositoryCatalog.Add([Repo("/a", "a")], Repo("/a", "x")));
    }

    [Test]
    public void Remove_DropsMatchingPath()
    {
        var result = GitRepositoryCatalog.Remove([Repo("/a", "a"), Repo("/b", "b")], "/A/");

        Assert.That(result.Select(r => r.Name), Is.EqualTo(new[] { "b" }));
    }

    [TestCase(1, -1, new[] { "b", "a", "c" })]
    [TestCase(0, 1, new[] { "b", "a", "c" })]
    [TestCase(0, -1, new[] { "a", "b", "c" })]
    [TestCase(2, 1, new[] { "a", "b", "c" })]
    public void Move_SwapsNeighbours_AndIgnoresOutOfRange(int index, int offset, string[] expected)
    {
        var result = GitRepositoryCatalog.Move([Repo("/a", "a"), Repo("/b", "b"), Repo("/c", "c")], index, offset);

        Assert.That(result.Select(r => r.Name), Is.EqualTo(expected));
    }

    [Test]
    public void EffectiveBranch_UsesRepositoryBranch_ThenProjectBranch()
    {
        var project = new ProjectConfig { DefaultBranch = "develop" };

        Assert.That(GitRepositoryCatalog.EffectiveBranch(Repo("/a", branch: "main"), project), Is.EqualTo("main"));
        Assert.That(GitRepositoryCatalog.EffectiveBranch(Repo("/a"), project), Is.EqualTo("develop"));
    }
}
