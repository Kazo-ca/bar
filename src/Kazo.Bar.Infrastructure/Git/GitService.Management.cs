namespace Kazo.Bar.Infrastructure.Git;

using Kazo.Bar.Core.Git;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

/// <summary>
/// Repository management (US5.7, US5.8): changes, diff, staging, commit, branches and worktrees.
/// Reading the branch list uses LibGit2Sharp; everything that changes the repository runs the
/// <c>git</c> CLI (hooks, filters and credentials apply). Paths given to Git are relative to the
/// repository root and passed as literal pathspecs, so file names are never read as patterns.
/// </summary>
public sealed partial class GitService
{
    /// <summary>Largest file read to count or show the lines of an untracked file.</summary>
    internal const long MaxUntrackedFileBytes = 2 * 1024 * 1024;

    /// <summary>Bytes looked at to decide whether a file is binary (same rule as Git).</summary>
    private const int BinaryProbeBytes = 8000;

    public async Task<GitChangeSet> GetChangesAsync(string localPath, CancellationToken cancellationToken = default)
    {
        var root = FindRoot(localPath);
        if (root == null)
            return GitChangeSet.Empty with { Error = $"Not a Git repository: {localPath}" };

        var status = RunAsync(root, ["-c", "core.quotepath=false", "status", "--porcelain=v1", "-z", "--untracked-files=all"], cancellationToken);
        var staged = RunAsync(root, ["diff", "--cached", "--numstat", "-z", "-M", "--no-ext-diff"], cancellationToken);
        var unstaged = RunAsync(root, ["diff", "--numstat", "-z", "--no-ext-diff"], cancellationToken);
        await Task.WhenAll(status, staged, unstaged);

        if (!status.Result.Success)
            return GitChangeSet.Empty with { Error = status.Result.Summary };

        return GitChangeParser.Parse(
            status.Result.Output,
            GitChangeParser.ParseNumstat(staged.Result.Output),
            GitChangeParser.ParseNumstat(unstaged.Result.Output),
            path => CountFileLines(Path.Combine(root, path)) is var (added, binary)
                ? new GitChangeParser.LineStat(binary ? null : added, binary ? null : 0)
                : null);
    }

    public async Task<GitFileDiff> GetFileDiffAsync(string localPath, GitFileChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        var root = FindRoot(localPath);
        if (root == null)
            return new GitFileDiff { Path = change.Path, Error = $"Not a Git repository: {localPath}" };

        if (change.Kind == GitChangeKind.Untracked)
            return await ReadNewFileDiffAsync(root, change.Path, cancellationToken);

        var arguments = new List<string> { "-c", "core.quotepath=false", "diff", "--no-color", "--no-ext-diff" };
        if (change.IsStaged) arguments.AddRange(["--cached", "-M"]);
        arguments.Add("--");
        arguments.Add(Literal(change.Path));
        if (change.OldPath != null) arguments.Add(Literal(change.OldPath));

        var result = await RunAsync(root, arguments.ToArray(), cancellationToken);
        return result.Success
            ? GitDiffParser.Parse(change.Path, result.Output)
            : new GitFileDiff { Path = change.Path, Error = result.Summary };
    }

    public Task<GitCommandResult> StageAsync(string localPath, IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        RunOnPathsAsync(localPath, ["add", "--all", "--"], paths, cancellationToken);

    public Task<GitCommandResult> UnstageAsync(string localPath, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        // Before the first commit there is no HEAD to restore from: take the files out of the index.
        string[] command = IsHeadUnborn(localPath) ? ["rm", "--cached", "-r", "--quiet", "--"] : ["restore", "--staged", "--"];
        return RunOnPathsAsync(localPath, command, paths, cancellationToken);
    }

    public async Task<GitCommandResult> DiscardAsync(string localPath, IReadOnlyList<GitFileChange> changes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var untracked = changes.Where(c => c.Kind == GitChangeKind.Untracked).Select(c => c.Path).ToList();
        var tracked = changes.Where(c => c.Kind != GitChangeKind.Untracked && !c.IsStaged).Select(c => c.Path).ToList();

        var result = Succeeded();
        if (tracked.Count > 0)
            result = await RunOnPathsAsync(localPath, ["restore", "--worktree", "--"], tracked, cancellationToken);
        if (result.Success && untracked.Count > 0)
            result = await RunOnPathsAsync(localPath, ["clean", "--force", "--"], untracked, cancellationToken);
        return result;
    }

    public async Task<GitCommandResult> CommitAsync(string localPath, string message, bool stageAll = false, bool amend = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Failed("The commit message is required.");
        var root = FindRoot(localPath);
        if (root == null)
            return Failed($"Not a Git repository: {localPath}");

        if (stageAll)
        {
            var add = await RunAsync(root, ["add", "--all"], cancellationToken);
            if (!add.Success) return add;
        }
        string[] arguments = amend
            ? ["commit", "--amend", "--message", message.Trim()]
            : ["commit", "--message", message.Trim()];
        return await RunAsync(root, arguments, cancellationToken);
    }

    public async Task<IReadOnlyList<GitBranch>> GetBranchesAsync(string localPath, CancellationToken cancellationToken = default)
    {
        var worktrees = await GetWorktreesAsync(localPath, cancellationToken);
        return await Task.Run(() => ReadBranches(localPath, worktrees), cancellationToken);
    }

    public async Task<GitCommandResult> CheckoutAsync(string localPath, GitBranch branch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (!branch.IsRemote)
            return await RunAsync(localPath, ["switch", branch.Name], cancellationToken);
        // A remote branch: use the local branch of that name when it exists, else create it.
        return LocalBranchExists(localPath, branch.LocalName)
            ? await RunAsync(localPath, ["switch", branch.LocalName], cancellationToken)
            : await RunAsync(localPath, ["switch", "--track", branch.Name], cancellationToken);
    }

    public Task<GitCommandResult> CreateBranchAsync(string localPath, string name, string? startPoint, bool checkout, CancellationToken cancellationToken = default)
    {
        if (GitBranchNames.Validate(name) is { } error)
            return Task.FromResult(Failed(error));
        var arguments = new List<string>(checkout ? ["switch", "--create", name.Trim()] : ["branch", name.Trim()]);
        if (!string.IsNullOrWhiteSpace(startPoint)) arguments.Add(startPoint.Trim());
        return RunAsync(localPath, arguments.ToArray(), cancellationToken);
    }

    public Task<GitCommandResult> DeleteBranchAsync(string localPath, string name, bool force = false, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(name) || name.StartsWith('-')
            ? Task.FromResult(Failed("Invalid branch name."))
            : RunAsync(localPath, ["branch", force ? "-D" : "-d", name], cancellationToken);

    public Task<GitCommandResult> FetchAsync(string localPath, CancellationToken cancellationToken = default) =>
        RunAsync(localPath, ["fetch", "--all", "--prune"], cancellationToken);

    public async Task<GitCommandResult> AddWorktreeAsync(string repositoryPath, string worktreePath, string sourceBranch, string? newBranch, CancellationToken cancellationToken = default)
    {
        if (GitWorktreeLocations.ValidateFolder(worktreePath) is { } folderError)
            return Failed(folderError);
        if (string.IsNullOrWhiteSpace(sourceBranch) || sourceBranch.StartsWith('-'))
            return Failed("Choose the source branch.");
        var target = worktreePath.Trim();
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            return Failed($"The folder {target} already exists and is not empty.");

        string[] arguments;
        if (!string.IsNullOrWhiteSpace(newBranch))
        {
            if (GitBranchNames.Validate(newBranch) is { } branchError)
                return Failed(branchError);
            arguments = ["worktree", "add", "-b", newBranch.Trim(), target, sourceBranch];
        }
        else if (RemoteBranchExists(repositoryPath, sourceBranch) && sourceBranch.IndexOf('/') is var slash and > 0)
        {
            var local = sourceBranch[(slash + 1)..];
            arguments = LocalBranchExists(repositoryPath, local)
                ? ["worktree", "add", target, local]
                : ["worktree", "add", "--track", "-b", local, target, sourceBranch];
        }
        else
        {
            arguments = ["worktree", "add", target, sourceBranch];
        }
        return await RunAsync(repositoryPath, arguments, cancellationToken);
    }

    public Task<GitCommandResult> RemoveWorktreeAsync(string repositoryPath, string worktreePath, bool force = false, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(worktreePath)
            ? Task.FromResult(Failed("The worktree folder is required."))
            : RunAsync(repositoryPath, force ? ["worktree", "remove", "--force", worktreePath] : ["worktree", "remove", worktreePath], cancellationToken);

    public Task<GitCommandResult> MoveWorktreeAsync(string repositoryPath, string worktreePath, string newPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return Task.FromResult(Failed("The worktree folder is required."));
        if (GitWorktreeLocations.ValidateFolder(newPath) is { } error)
            return Task.FromResult(Failed(error));
        if (Directory.Exists(newPath.Trim()))
            return Task.FromResult(Failed($"The folder {newPath.Trim()} already exists."));
        var parent = Path.GetDirectoryName(newPath.Trim().TrimEnd('/', '\\'));
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        return RunAsync(repositoryPath, ["worktree", "move", worktreePath, newPath.Trim()], cancellationToken);
    }

    private IReadOnlyList<GitBranch> ReadBranches(string localPath, IReadOnlyList<GitWorktree> worktrees)
    {
        try
        {
            var gitDirectory = Repository.Discover(localPath);
            if (gitDirectory == null) return Array.Empty<GitBranch>();
            using var repository = new Repository(gitDirectory);
            var list = new List<GitBranch>();
            foreach (var branch in repository.Branches)
            {
                if (branch.IsRemote && branch.CanonicalName.EndsWith("/HEAD", StringComparison.Ordinal)) continue;
                var tracking = !branch.IsRemote && branch.IsTracking ? branch.TrackingDetails : null;
                var tip = branch.Tip;
                var otherWorktree = branch.IsRemote || branch.IsCurrentRepositoryHead
                    ? null
                    : worktrees.FirstOrDefault(w => w.Branch == branch.FriendlyName)?.Path;
                list.Add(new GitBranch
                {
                    Name = branch.FriendlyName,
                    IsRemote = branch.IsRemote,
                    IsCurrent = branch.IsCurrentRepositoryHead,
                    CheckedOutIn = otherWorktree,
                    Upstream = !branch.IsRemote && branch.IsTracking ? branch.TrackedBranch?.FriendlyName : null,
                    Ahead = tracking?.AheadBy,
                    Behind = tracking?.BehindBy,
                    Tip = tip?.Sha[..ShortShaLength],
                    TipMessage = tip?.MessageShort,
                    TipDate = tip?.Committer.When,
                });
            }
            return list
                .OrderBy(b => b.IsRemote)
                .ThenByDescending(b => b.IsCurrent)
                .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (LibGit2SharpException ex)
        {
            _logger.LogWarning(ex, "Could not read the branches of {Path}", localPath);
            return Array.Empty<GitBranch>();
        }
    }

    private async Task<GitCommandResult> RunOnPathsAsync(string localPath, string[] command, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0) return Succeeded();
        var root = FindRoot(localPath);
        if (root == null) return Failed($"Not a Git repository: {localPath}");
        return await RunAsync(root, command.Concat(paths.Select(Literal)).ToArray(), cancellationToken);
    }

    private static async Task<GitFileDiff> ReadNewFileDiffAsync(string root, string path, CancellationToken cancellationToken)
    {
        var full = Path.Combine(root, path);
        try
        {
            var info = new FileInfo(full);
            if (!info.Exists) return new GitFileDiff { Path = path, Error = "The file no longer exists." };
            if (info.Length > MaxUntrackedFileBytes) return new GitFileDiff { Path = path, IsTruncated = true, Error = "The file is too large to show." };
            var bytes = await File.ReadAllBytesAsync(full, cancellationToken);
            if (IsBinary(bytes)) return new GitFileDiff { Path = path, IsBinary = true };
            return GitDiffParser.ForNewFile(path, System.Text.Encoding.UTF8.GetString(bytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new GitFileDiff { Path = path, Error = ex.Message };
        }
    }

    /// <summary>Line count of a new file, or null when it cannot be read; binary files are flagged.</summary>
    internal static (int Lines, bool Binary)? CountFileLines(string fullPath)
    {
        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length > MaxUntrackedFileBytes) return null;
            var bytes = File.ReadAllBytes(fullPath);
            if (IsBinary(bytes)) return (0, true);
            if (bytes.Length == 0) return (0, false);
            var lines = bytes.Count(b => b == (byte)'\n');
            if (bytes[^1] != (byte)'\n') lines++;
            return (lines, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsBinary(byte[] bytes) =>
        Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, BinaryProbeBytes)) >= 0;

    /// <summary>
    /// Lines added and removed by the local changes, for the toolbar counter: the index and the
    /// working tree compared with HEAD (LibGit2Sharp includes the untracked files).
    /// </summary>
    private (int Added, int Deleted) CountChangedLines(Repository repository)
    {
        try
        {
            var stats = repository.Diff.Compare<PatchStats>(repository.Head.Tip?.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory);
            return (stats.TotalLinesAdded, stats.TotalLinesDeleted);
        }
        catch (LibGit2SharpException ex)
        {
            _logger.LogDebug(ex, "Could not count the changed lines of {Path}", repository.Info.WorkingDirectory);
            return (0, 0);
        }
    }

    /// <summary>Root folder of the working tree the folder belongs to; null outside a repository.</summary>
    private string? FindRoot(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath)) return null;
        try
        {
            var gitDirectory = Repository.Discover(localPath);
            if (gitDirectory == null) return null;
            using var repository = new Repository(gitDirectory);
            return repository.Info.WorkingDirectory?.TrimEnd('/', '\\');
        }
        catch (LibGit2SharpException ex)
        {
            _logger.LogDebug(ex, "No repository at {Path}", localPath);
            return null;
        }
    }

    private bool IsHeadUnborn(string localPath) => WithRepository(localPath, r => r.Info.IsHeadUnborn);

    private bool LocalBranchExists(string localPath, string name) =>
        WithRepository(localPath, r => r.Branches[name] is { IsRemote: false });

    private bool RemoteBranchExists(string localPath, string name) =>
        WithRepository(localPath, r => r.Branches[name] is { IsRemote: true });

    private bool WithRepository(string localPath, Func<Repository, bool> read)
    {
        try
        {
            var gitDirectory = Directory.Exists(localPath) ? Repository.Discover(localPath) : null;
            if (gitDirectory == null) return false;
            using var repository = new Repository(gitDirectory);
            return read(repository);
        }
        catch (LibGit2SharpException)
        {
            return false;
        }
    }

    /// <summary>A pathspec that matches exactly this path, relative to the repository root.</summary>
    private static string Literal(string path) => ":(top,literal)" + path;

    private static GitCommandResult Succeeded() => new(true, 0, string.Empty, string.Empty);

    private static GitCommandResult Failed(string message) => new(false, -1, string.Empty, message);
}
