namespace Kazo.Bar.Plugins.Git;

/// <summary>Tabs of the Git repository manager (US5.7, US5.8).</summary>
public enum GitManagerTab
{
    /// <summary>Local changes, their diff and the commit.</summary>
    Changes,

    /// <summary>Branches: switch, create, delete.</summary>
    Branches,

    /// <summary>Worktrees: create from a source branch, move, delete, and where they are created.</summary>
    Worktrees,
}
