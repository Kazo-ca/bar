namespace DevToolbar.Tests.Unit.Git;

using DevToolbar.Core.Git;

[TestFixture]
public class GitWorktreeParserTests
{
    [Test]
    public void Parses_MainAndLinkedWorktrees()
    {
        const string output = "worktree C:/src/api\r\nHEAD 1234567890abcdef\r\nbranch refs/heads/main\r\n\r\n"
                            + "worktree C:/src/api-hotfix\nHEAD abcdef1234567890\nbranch refs/heads/hotfix/login\nlocked\n\n"
                            + "worktree C:/src/api-review\nHEAD fedcba9876543210\ndetached\n";

        var list = GitWorktreeParser.Parse(output);

        Assert.That(list, Has.Count.EqualTo(3));
        Assert.That(list[0].IsMain, Is.True);
        Assert.That(list[0].Path, Is.EqualTo("C:/src/api"));
        Assert.That(list[0].Branch, Is.EqualTo("main"));
        Assert.That(list[0].Head, Is.EqualTo("1234567"));
        Assert.That(list[1].Branch, Is.EqualTo("hotfix/login"));
        Assert.That(list[1].Name, Is.EqualTo("api-hotfix"));
        Assert.That(list[1].IsMain, Is.False);
        Assert.That(list[2].IsDetached, Is.True);
    }

    [Test]
    public void BareMainRepository_IsLeftOut_ButStaysTheMainOne()
    {
        const string output = "worktree /srv/repo.git\nbare\n\nworktree /srv/wt\nHEAD 1111111111\nbranch refs/heads/dev\n";

        var list = GitWorktreeParser.Parse(output);

        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Path, Is.EqualTo("/srv/wt"));
        Assert.That(list[0].IsMain, Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \n ")]
    public void EmptyOutput_GivesNoWorktree(string? output)
    {
        Assert.That(GitWorktreeParser.Parse(output), Is.Empty);
    }
}
