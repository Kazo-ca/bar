namespace DevToolbar.Infrastructure.Git;

using System.Diagnostics;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real <see cref="IGitService"/> (US5.1): the state is read from the local folder with
/// LibGit2Sharp; pull and push run the <c>git</c> CLI so the user's credential helper and
/// Git configuration apply.
/// </summary>
public sealed class GitService : IGitService
{
    /// <summary>Longest time a pull or push may take before it is stopped.</summary>
    public static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromMinutes(2);

    private const int ShortShaLength = 7;

    private readonly ILogger _logger;
    private readonly string _gitExecutable;
    private readonly TimeSpan _commandTimeout;

    public GitService(ILogger<GitService>? logger = null)
        : this(logger, "git", DefaultCommandTimeout)
    {
    }

    internal GitService(ILogger<GitService>? logger, string gitExecutable, TimeSpan commandTimeout)
    {
        _logger = logger ?? NullLogger<GitService>.Instance;
        _gitExecutable = gitExecutable;
        _commandTimeout = commandTimeout;
    }

    public Task<GitStatus> GetStatusAsync(string localPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadStatus(localPath), cancellationToken);

    public Task<GitCommandResult> PullAsync(string localPath, CancellationToken cancellationToken = default) =>
        RunAsync(localPath, ["pull"], cancellationToken);

    public Task<GitCommandResult> PushAsync(string localPath, CancellationToken cancellationToken = default) =>
        RunAsync(localPath, ["push"], cancellationToken);

    private GitStatus ReadStatus(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
            return GitStatus.Problem(GitStatusKind.FolderNotFound, $"Folder not found: {localPath}");

        try
        {
            var gitDirectory = Repository.Discover(localPath);
            if (gitDirectory == null)
                return GitStatus.Problem(GitStatusKind.NotARepository, $"Not a Git repository: {localPath}");

            using var repository = new Repository(gitDirectory);
            var head = repository.Head;
            var detached = repository.Info.IsHeadDetached;
            var status = repository.RetrieveStatus(new StatusOptions
            {
                IncludeUntracked = true,
                RecurseUntrackedDirs = false,
                IncludeIgnored = false,
            });
            var tracking = head.IsTracking ? head.TrackingDetails : null;

            return new GitStatus
            {
                Kind = GitStatusKind.Repository,
                Branch = detached ? head.Tip?.Sha[..ShortShaLength] : head.FriendlyName,
                IsDetached = detached,
                ChangedFiles = status.Count(e => e.State != FileStatus.Unaltered && e.State != FileStatus.Ignored),
                Ahead = tracking?.AheadBy,
                Behind = tracking?.BehindBy,
            };
        }
        catch (LibGit2SharpException ex)
        {
            _logger.LogWarning(ex, "Could not read Git status of {Path}", localPath);
            return GitStatus.Problem(GitStatusKind.Error, ex.Message);
        }
    }

    private async Task<GitCommandResult> RunAsync(string localPath, string[] arguments, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
            return new GitCommandResult(false, -1, string.Empty, $"Folder not found: {localPath}");

        var startInfo = new ProcessStartInfo(_gitExecutable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        // ArgumentList quotes each value: no injection through the folder path.
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(localPath);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        // Fail instead of waiting forever for a credential prompt nobody can see.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_commandTimeout);
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Could not start {_gitExecutable}.");
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                var reason = cancellationToken.IsCancellationRequested ? "cancelled" : $"timed out after {_commandTimeout.TotalSeconds:0} s";
                return new GitCommandResult(false, -1, string.Empty, $"git {string.Join(' ', arguments)} {reason}.");
            }
            return new GitCommandResult(process.ExitCode == 0, process.ExitCode, await output, await error);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogError(ex, "Could not run git in {Path}", localPath);
            return new GitCommandResult(false, -1, string.Empty, $"Could not run git: {ex.Message}. Is Git installed and on the PATH?");
        }
    }
}
