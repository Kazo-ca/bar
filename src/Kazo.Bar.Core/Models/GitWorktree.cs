namespace Kazo.Bar.Core.Models;

/// <summary>
/// A working tree of a repository, as listed by <c>git worktree list</c> (US5.6).
/// The main working tree is the repository folder itself; the others are linked worktrees.
/// </summary>
public sealed record GitWorktree
{
    /// <summary>Folder of the working tree.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Checked-out branch (short name), or null when the HEAD is detached.</summary>
    public string? Branch { get; init; }

    /// <summary>Short commit id of the HEAD.</summary>
    public string? Head { get; init; }

    /// <summary>True for the repository's main working tree (the first one listed).</summary>
    public bool IsMain { get; init; }

    public bool IsDetached => Branch == null;

    /// <summary>Name shown in the toolbar: the folder name.</summary>
    public string Name => Configuration.RepositoryPaths.NameFromPath(Path);
}
