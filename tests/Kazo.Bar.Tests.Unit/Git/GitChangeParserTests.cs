namespace Kazo.Bar.Tests.Unit.Git;

using Kazo.Bar.Core.Git;
using Kazo.Bar.Core.Models;

[TestFixture]
public class GitChangeParserTests
{
    [Test]
    public void Numstat_ReadsCounts_RenamesAndBinaries()
    {
        var stats = GitChangeParser.ParseNumstat("3\t1\tsrc/a.cs\0-\t-\tlogo.png\0" + "2\t0\t\0old.txt\0new.txt\0");

        Assert.That(stats["src/a.cs"], Is.EqualTo(new GitChangeParser.LineStat(3, 1)));
        Assert.That(stats["logo.png"].IsBinary, Is.True);
        Assert.That(stats["new.txt"], Is.EqualTo(new GitChangeParser.LineStat(2, 0)));
        Assert.That(stats.ContainsKey("old.txt"), Is.False);
    }

    [Test]
    public void Status_SplitsStagedAndUnstaged_WithLineCounts()
    {
        const string porcelain = "MM src/a.cs\0A  added.txt\0 D gone.txt\0R  new.txt\0old.txt\0?? notes.md\0UU conflict.cs\0";
        var staged = GitChangeParser.ParseNumstat("1\t0\tsrc/a.cs\0" + "4\t0\tadded.txt\0");
        var unstaged = GitChangeParser.ParseNumstat("2\t2\tsrc/a.cs\0" + "0\t7\tgone.txt\0");

        var changes = GitChangeParser.Parse(porcelain, staged, unstaged, path => path == "notes.md" ? new(5, 0) : null);

        Assert.That(changes.Staged.Select(c => (c.Path, c.Kind)), Is.EqualTo(new[]
        {
            ("added.txt", GitChangeKind.Added),
            ("new.txt", GitChangeKind.Renamed),
            ("src/a.cs", GitChangeKind.Modified),
        }));
        Assert.That(changes.Staged.All(c => c.IsStaged), Is.True);
        Assert.That(changes.Staged.Single(c => c.Path == "new.txt").OldPath, Is.EqualTo("old.txt"));
        Assert.That(changes.Unstaged.Select(c => (c.Path, c.Kind)), Is.EqualTo(new[]
        {
            ("conflict.cs", GitChangeKind.Conflicted),
            ("gone.txt", GitChangeKind.Deleted),
            ("notes.md", GitChangeKind.Untracked),
            ("src/a.cs", GitChangeKind.Modified),
        }));
        Assert.That(changes.HasConflicts, Is.True);
        Assert.That(changes.LinesAdded, Is.EqualTo(1 + 4 + 2 + 5));
        Assert.That(changes.LinesDeleted, Is.EqualTo(2 + 7));
    }

    [Test]
    public void Status_Empty_HasNoChanges()
    {
        Assert.That(GitChangeParser.Parse("").HasChanges, Is.False);
        Assert.That(GitChangeParser.Parse(null), Is.SameAs(GitChangeSet.Empty));
    }

    [Test]
    public void FileChange_SplitsFolderAndName()
    {
        var change = new GitFileChange { Path = "src/app/main.cs" };

        Assert.That(change.FileName, Is.EqualTo("main.cs"));
        Assert.That(change.Folder, Is.EqualTo("src/app"));
        Assert.That(new GitFileChange { Path = "readme.md" }.Folder, Is.Empty);
    }
}
