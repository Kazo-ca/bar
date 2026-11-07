namespace Kazo.Bar.Core.Models;

/// <summary>Kind of a line in a diff.</summary>
public enum GitDiffLineKind
{
    Context,
    Added,
    Deleted,

    /// <summary>"\ No newline at end of file" and similar notes.</summary>
    Note,
}

/// <summary>A line of a diff, with its numbers in the old and new versions of the file.</summary>
public sealed record GitDiffLine(GitDiffLineKind Kind, int? OldNumber, int? NewNumber, string Text);

/// <summary>A block of changed lines with some context around them.</summary>
public sealed record GitDiffHunk(string Header, IReadOnlyList<GitDiffLine> Lines);

/// <summary>The changes of one file, ready to be shown line by line (US5.7).</summary>
public sealed record GitFileDiff
{
    public string Path { get; init; } = string.Empty;

    public bool IsBinary { get; init; }

    public IReadOnlyList<GitDiffHunk> Hunks { get; init; } = Array.Empty<GitDiffHunk>();

    /// <summary>User-facing reason when the diff could not be read; null otherwise.</summary>
    public string? Error { get; init; }

    /// <summary>True when the diff was cut because the file is too large to show.</summary>
    public bool IsTruncated { get; init; }

    public int LinesAdded => Hunks.Sum(h => h.Lines.Count(l => l.Kind == GitDiffLineKind.Added));

    public int LinesDeleted => Hunks.Sum(h => h.Lines.Count(l => l.Kind == GitDiffLineKind.Deleted));
}
