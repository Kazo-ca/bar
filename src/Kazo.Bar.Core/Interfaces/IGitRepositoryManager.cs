namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>
/// Manages a repository from the toolbar (US5.7, US5.8): local changes and their diff, staging,
/// commit, branches and worktrees. Every command runs in the given folder (the repository or one
/// of its worktrees). Commands that change the repository go through the <c>git</c> CLI so the
/// user's configuration, hooks, filters (LFS) and credential helper apply. Methods never throw
/// for a Git failure: they return a failed <see cref="GitCommandResult"/> with Git's message.
/// </summary>
public interface IGitRepositoryManager
{
    /// <summary>Staged and not staged changes with their line counts.</summary>
    Task<GitChangeSet> GetChangesAsync(string localPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// The diff of one file: of the index against HEAD when <paramref name="staged"/> is true,
    /// else of the working tree against the index (an untracked file shows all its lines as added).
    /// </summary>
    Task<GitFileDiff> GetFileDiffAsync(string localPath, GitFileChange change, CancellationToken cancellationToken = default);

    /// <summary>Adds the files to the index (<c>git add</c>).</summary>
    Task<GitCommandResult> StageAsync(string localPath, IReadOnlyList<string> paths, CancellationToken cancellationToken = default);

    /// <summary>Removes the files from the index, keeping the working tree (<c>git restore --staged</c>).</summary>
    Task<GitCommandResult> UnstageAsync(string localPath, IReadOnlyList<string> paths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws away the working-tree changes of the files (<c>git restore</c>); untracked files are
    /// deleted (<c>git clean</c>). This cannot be undone.
    /// </summary>
    Task<GitCommandResult> DiscardAsync(string localPath, IReadOnlyList<GitFileChange> changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the staged changes (<c>git commit</c>). With <paramref name="stageAll"/>, every
    /// change (untracked files included) is staged first. <paramref name="amend"/> rewrites the
    /// last commit.
    /// </summary>
    Task<GitCommandResult> CommitAsync(string localPath, string message, bool stageAll = false, bool amend = false, CancellationToken cancellationToken = default);

    /// <summary>Local then remote-tracking branches, the checked-out one flagged.</summary>
    Task<IReadOnlyList<GitBranch>> GetBranchesAsync(string localPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks out a branch in the folder (<c>git switch</c>). A remote branch (<c>origin/x</c>)
    /// is checked out as a local branch following it, or the existing local branch of that name.
    /// </summary>
    Task<GitCommandResult> CheckoutAsync(string localPath, GitBranch branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates <paramref name="name"/> from <paramref name="startPoint"/> (a branch or commit; null
    /// for the current HEAD) and checks it out when <paramref name="checkout"/> is true.
    /// </summary>
    Task<GitCommandResult> CreateBranchAsync(string localPath, string name, string? startPoint, bool checkout, CancellationToken cancellationToken = default);

    /// <summary>Deletes a local branch (<c>git branch -d</c>, or <c>-D</c> with <paramref name="force"/>).</summary>
    Task<GitCommandResult> DeleteBranchAsync(string localPath, string name, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>Updates the remote-tracking branches (<c>git fetch --all --prune</c>).</summary>
    Task<GitCommandResult> FetchAsync(string localPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a worktree at <paramref name="worktreePath"/> (<c>git worktree add</c>). With
    /// <paramref name="newBranch"/>, a new branch of that name is created from
    /// <paramref name="sourceBranch"/>; otherwise <paramref name="sourceBranch"/> (a local branch,
    /// or a remote one that gets its local branch) is checked out in it.
    /// </summary>
    Task<GitCommandResult> AddWorktreeAsync(string repositoryPath, string worktreePath, string sourceBranch, string? newBranch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a linked worktree and its folder (<c>git worktree remove</c>). Without
    /// <paramref name="force"/>, Git refuses when the worktree has local changes.
    /// </summary>
    Task<GitCommandResult> RemoveWorktreeAsync(string repositoryPath, string worktreePath, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>Moves a linked worktree to another folder (<c>git worktree move</c>).</summary>
    Task<GitCommandResult> MoveWorktreeAsync(string repositoryPath, string worktreePath, string newPath, CancellationToken cancellationToken = default);
}
