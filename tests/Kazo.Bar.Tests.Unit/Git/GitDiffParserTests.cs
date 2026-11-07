namespace Kazo.Bar.Tests.Unit.Git;

using Kazo.Bar.Core.Git;
using Kazo.Bar.Core.Models;

[TestFixture]
public class GitDiffParserTests
{
    [Test]
    public void UnifiedDiff_IsReadIntoNumberedLines()
    {
        const string diff = """
            diff --git a/a.txt b/a.txt
            index 1111111..2222222 100644
            --- a/a.txt
            +++ b/a.txt
            @@ -1,3 +1,3 @@ header
             one
            -two
            +deux
             three
            @@ -10,1 +10,2 @@
             ten
            +eleven
            \ No newline at end of file

            """;

        var result = GitDiffParser.Parse("a.txt", diff);

        Assert.That(result.Hunks, Has.Count.EqualTo(2));
        var first = result.Hunks[0].Lines;
        Assert.That(first.Select(l => l.Kind), Is.EqualTo(new[] { GitDiffLineKind.Context, GitDiffLineKind.Deleted, GitDiffLineKind.Added, GitDiffLineKind.Context }));
        Assert.That(first[1], Is.EqualTo(new GitDiffLine(GitDiffLineKind.Deleted, 2, null, "two")));
        Assert.That(first[2], Is.EqualTo(new GitDiffLine(GitDiffLineKind.Added, null, 2, "deux")));
        Assert.That(first[3], Is.EqualTo(new GitDiffLine(GitDiffLineKind.Context, 3, 3, "three")));
        Assert.That(result.Hunks[1].Lines[1].NewNumber, Is.EqualTo(11));
        Assert.That(result.Hunks[1].Lines[2].Kind, Is.EqualTo(GitDiffLineKind.Note));
        Assert.That(result.LinesAdded, Is.EqualTo(2));
        Assert.That(result.LinesDeleted, Is.EqualTo(1));
    }

    [Test]
    public void BinaryDiff_IsFlagged()
    {
        var result = GitDiffParser.Parse("logo.png", "diff --git a/logo.png b/logo.png\nBinary files a/logo.png and b/logo.png differ\n");

        Assert.That(result.IsBinary, Is.True);
        Assert.That(result.Hunks, Is.Empty);
    }

    [Test]
    public void CombinedDiff_OfAConflict_IsRead()
    {
        const string diff = "diff --cc a.txt\n@@@ -1,1 -1,1 +1,5 @@@\n++<<<<<<< HEAD\n +mine\n++=======\n+ theirs\n++>>>>>>> other\n";

        var result = GitDiffParser.Parse("a.txt", diff);

        Assert.That(result.Hunks.Single().Lines, Has.Count.EqualTo(5));
        Assert.That(result.Hunks[0].Lines[1], Is.EqualTo(new GitDiffLine(GitDiffLineKind.Added, null, 2, "mine")));
    }

    [Test]
    public void NewFile_ShowsEveryLineAsAdded()
    {
        var result = GitDiffParser.ForNewFile("n.txt", "a\r\nb\n");

        Assert.That(result.Hunks.Single().Lines.Select(l => (l.Kind, l.NewNumber, l.Text)), Is.EqualTo(new[]
        {
            (GitDiffLineKind.Added, (int?)1, "a"),
            (GitDiffLineKind.Added, (int?)2, "b"),
        }));
    }

    [Test]
    public void HugeDiff_IsTruncated()
    {
        var content = string.Join('\n', Enumerable.Range(0, GitDiffParser.MaxLines + 10)) + "\n";

        var result = GitDiffParser.ForNewFile("big.txt", content);

        Assert.That(result.IsTruncated, Is.True);
        Assert.That(result.LinesAdded, Is.EqualTo(GitDiffParser.MaxLines));
    }
}
