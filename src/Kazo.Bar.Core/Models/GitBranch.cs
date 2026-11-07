namespace Kazo.Bar.Core.Models;

/// <summary>A local or remote-tracking branch of a repository (US5.7).</summary>
public sealed record GitBranch
{
    /// <summary>Short name: <c>feature/login</c> for a local branch, <c>origin/feature/login</c> for a remote one.</summary>
    public string Name { get; init; } = string.Empty;

    public bool IsRemote { get; init; }

    /// <summary>True for the branch checked out in the folder that was read.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Folder of another worktree where this local branch is checked out; null otherwise.</summary>
    public string? CheckedOutIn { get; init; }

    /// <summary>Remote branch this local branch follows (<c>origin/main</c>); null without upstream.</summary>
    public string? Upstream { get; init; }

    /// <summary>Commits not pushed to <see cref="Upstream"/>; null without upstream.</summary>
    public int? Ahead { get; init; }

    /// <summary>Commits of <see cref="Upstream"/> not pulled yet; null without upstream.</summary>
    public int? Behind { get; init; }

    /// <summary>Short commit id of the branch tip.</summary>
    public string? Tip { get; init; }

    /// <summary>First line of the tip commit's message.</summary>
    public string? TipMessage { get; init; }

    public DateTimeOffset? TipDate { get; init; }

    /// <summary>
    /// Local name a remote branch gets when it is checked out (<c>origin/feature/x</c> gives
    /// <c>feature/x</c>); the name itself for a local branch.
    /// </summary>
    public string LocalName => IsRemote && Name.IndexOf('/') is var i and > 0 ? Name[(i + 1)..] : Name;
}
