namespace DevToolbar.Core.Interfaces;

using DevToolbar.Core.Models;

/// <summary>
/// Reads the state of a repository's local folder and runs pull / push in it (US5.1).
/// Real implementation: LibGit2Sharp for reading, the <c>git</c> CLI for network commands
/// (so the user's credential helper is used). The Web host uses a simulated one.
/// </summary>
public interface IGitService
{
    /// <summary>Reads branch, local changes and ahead/behind counts. Never throws for a bad folder.</summary>
    Task<GitStatus> GetStatusAsync(string localPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the working trees of the repository the folder belongs to (main one first, US5.6).
    /// Returns an empty list when the folder is not a repository or Git cannot be run.
    /// </summary>
    Task<IReadOnlyList<GitWorktree>> GetWorktreesAsync(string localPath, CancellationToken cancellationToken = default);

    /// <summary>Runs <c>git pull</c> in the folder.</summary>
    Task<GitCommandResult> PullAsync(string localPath, CancellationToken cancellationToken = default);

    /// <summary>Runs <c>git push</c> in the folder.</summary>
    Task<GitCommandResult> PushAsync(string localPath, CancellationToken cancellationToken = default);
}
