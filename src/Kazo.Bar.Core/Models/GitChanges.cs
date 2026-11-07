namespace Kazo.Bar.Core.Models;

/// <summary>What happened to a file, as reported by <c>git status</c>.</summary>
public enum GitChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,

    /// <summary>A new file Git does not track yet.</summary>
    Untracked,

    /// <summary>A file left with merge conflicts to resolve.</summary>
    Conflicted,
}

/// <summary>
/// A changed file of a working copy, either in the index (staged) or in the working tree
/// (not staged yet). A file changed in both places appears once in each list of
/// <see cref="GitChangeSet"/>.
/// </summary>
public sealed record GitFileChange
{
    /// <summary>Path relative to the repository root, with <c>/</c> separators.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Previous path of a renamed or copied file; null otherwise.</summary>
    public string? OldPath { get; init; }

    public GitChangeKind Kind { get; init; }

    /// <summary>True for a change recorded in the index (part of the next commit).</summary>
    public bool IsStaged { get; init; }

    /// <summary>Lines added; 0 for a binary file.</summary>
    public int LinesAdded { get; init; }

    /// <summary>Lines removed; 0 for a binary file.</summary>
    public int LinesDeleted { get; init; }

    /// <summary>True when Git cannot show the file's lines (images, archives...).</summary>
    public bool IsBinary { get; init; }

    /// <summary>File name without its folder.</summary>
    public string FileName => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>Folder of the file (empty at the repository root).</summary>
    public string Folder => Path.LastIndexOf('/') is var i and >= 0 ? Path[..i] : string.Empty;
}

/// <summary>Every local change of a working copy, split in staged and not staged (US5.7).</summary>
public sealed record GitChangeSet
{
    public static readonly GitChangeSet Empty = new();

    /// <summary>Changes in the index: what the next commit records.</summary>
    public IReadOnlyList<GitFileChange> Staged { get; init; } = Array.Empty<GitFileChange>();

    /// <summary>Changes in the working tree that are not staged, untracked files and conflicts.</summary>
    public IReadOnlyList<GitFileChange> Unstaged { get; init; } = Array.Empty<GitFileChange>();

    /// <summary>User-facing reason when the changes could not be read; null otherwise.</summary>
    public string? Error { get; init; }

    public bool HasChanges => Staged.Count > 0 || Unstaged.Count > 0;

    public bool HasConflicts => Unstaged.Any(c => c.Kind == GitChangeKind.Conflicted);

    public int LinesAdded => Staged.Sum(c => c.LinesAdded) + Unstaged.Sum(c => c.LinesAdded);

    public int LinesDeleted => Staged.Sum(c => c.LinesDeleted) + Unstaged.Sum(c => c.LinesDeleted);
}
