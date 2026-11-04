namespace Kazo.Bar.Core.Models;

/// <summary>What was found in a repository's local folder.</summary>
public enum GitStatusKind
{
    /// <summary>A Git working copy; <see cref="GitStatus.Branch"/> and the counters are set.</summary>
    Repository,

    /// <summary>The configured folder does not exist.</summary>
    FolderNotFound,

    /// <summary>The folder exists but is not inside a Git working copy.</summary>
    NotARepository,

    /// <summary>The repository could not be read; see <see cref="GitStatus.Error"/>.</summary>
    Error,
}

/// <summary>
/// State of a local working copy, read from disk (US5.1). Immutable snapshot.
/// </summary>
public sealed record GitStatus
{
    public GitStatusKind Kind { get; init; }

    /// <summary>Current branch name, or the short commit id when the HEAD is detached.</summary>
    public string? Branch { get; init; }

    public bool IsDetached { get; init; }

    /// <summary>Files added, modified, deleted, renamed or untracked (ignored files excluded).</summary>
    public int ChangedFiles { get; init; }

    /// <summary>Commits not pushed to the tracked branch; null without upstream.</summary>
    public int? Ahead { get; init; }

    /// <summary>Commits on the tracked branch not pulled yet (as of the last fetch); null without upstream.</summary>
    public int? Behind { get; init; }

    /// <summary>User-facing reason when <see cref="Kind"/> is not <see cref="GitStatusKind.Repository"/>.</summary>
    public string? Error { get; init; }

    public bool IsRepository => Kind == GitStatusKind.Repository;

    public bool IsDirty => ChangedFiles > 0;

    public static GitStatus Problem(GitStatusKind kind, string message) => new() { Kind = kind, Error = message };
}

/// <summary>Outcome of a Git command such as pull or push.</summary>
public sealed record GitCommandResult(bool Success, int ExitCode, string Output, string Error)
{
    /// <summary>True when the output reports merge conflicts.</summary>
    public bool HasConflicts =>
        Output.Contains("CONFLICT", StringComparison.Ordinal) || Error.Contains("CONFLICT", StringComparison.Ordinal);

    /// <summary>The most useful line to show the user (last error line, else last output line).</summary>
    public string Summary
    {
        get
        {
            static string? LastLine(string text) => text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault();
            return (Success ? LastLine(Output) ?? LastLine(Error) : LastLine(Error) ?? LastLine(Output)) ?? string.Empty;
        }
    }
}
