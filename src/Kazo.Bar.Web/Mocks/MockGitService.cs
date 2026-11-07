namespace Kazo.Bar.Web.Mocks;

using Kazo.Bar.Core.Git;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Plugins.Git;

/// <summary>
/// Simulated Git for the Web test host: no folder is read and no command runs. Every configured
/// repository starts as a clean working copy on its configured branch. The first repository of
/// the sample project has two linked worktrees; the "hotfix" one has two local changes so the
/// changes view, the diff and the commit can be seen and tested (US5.6, US5.7, US5.8). Branches,
/// worktrees, staging and commits change this in-memory state only.
/// </summary>
public class MockGitService : IGitService, IGitRepositoryManager
{
    private readonly ISettingsService _settings;
    private readonly object _gate = new();
    private readonly Dictionary<string, RepositoryState> _repositories = new(StringComparer.OrdinalIgnoreCase);

    public MockGitService(ISettingsService settings)
    {
        _settings = settings;
    }

    private sealed class RepositoryState
    {
        public required string MainPath { get; init; }
        public List<string> LocalBranches { get; } = new();
        public List<string> RemoteBranches { get; } = new();

        /// <summary>Worktree folders (main first) and the branch checked out in each.</summary>
        public List<(string Path, string Branch, string Head)> Worktrees { get; } = new();

        /// <summary>Local changes by worktree folder.</summary>
        public Dictionary<string, List<GitFileChange>> Changes { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly (string Suffix, string Branch, string Head)[] _linked =
    [
        ("-hotfix", "hotfix/login-redirect", "9f8e7d6"),
        ("-review", "feature/very-long-branch-name-for-the-review-of-the-new-dashboard", "5c4b3a2"),
    ];

    private static string LinkedPath(string mainPath, string suffix) => mainPath.TrimEnd('/', '\\') + suffix;

    private static List<GitFileChange> SampleChanges() =>
    [
        new() { Path = "src/Auth/LoginController.cs", Kind = GitChangeKind.Modified, LinesAdded = 3, LinesDeleted = 2 },
        new() { Path = "src/Auth/RedirectGuard.cs", Kind = GitChangeKind.Untracked, LinesAdded = 9 },
    ];

    /// <summary>Finds (or builds on first use) the state of the repository a folder belongs to.</summary>
    private async Task<(RepositoryState State, string Path)?> FindAsync(string localPath)
    {
        foreach (var project in await _settings.GetProjectsAsync())
        {
            var repositories = GitRepositoryCatalog.Resolve(project);
            foreach (var repository in repositories)
            {
                var state = StateOf(repository, project, repository == repositories[0]);
                lock (_gate)
                {
                    var worktree = state.Worktrees.FirstOrDefault(w => GitRepositoryCatalog.SamePath(w.Path, localPath));
                    if (worktree.Path != null) return (state, worktree.Path);
                }
            }
        }
        return null;
    }

    private RepositoryState StateOf(GitRepository repository, ProjectConfig project, bool withWorktrees)
    {
        lock (_gate)
        {
            if (_repositories.TryGetValue(repository.Path, out var existing)) return existing;
            var branch = GitRepositoryCatalog.EffectiveBranch(repository, project);
            var state = new RepositoryState { MainPath = repository.Path };
            state.LocalBranches.Add(branch);
            state.RemoteBranches.AddRange([$"origin/{branch}", "origin/release/2.0"]);
            state.Worktrees.Add((repository.Path, branch, "a1b2c3d"));
            if (withWorktrees)
            {
                foreach (var (suffix, linkedBranch, head) in _linked)
                {
                    state.LocalBranches.Add(linkedBranch);
                    state.Worktrees.Add((LinkedPath(repository.Path, suffix), linkedBranch, head));
                }
                state.Changes[LinkedPath(repository.Path, _linked[0].Suffix)] = SampleChanges();
            }
            _repositories[repository.Path] = state;
            return state;
        }
    }

    private List<GitFileChange> ChangesOf(RepositoryState state, string path)
    {
        if (!state.Changes.TryGetValue(path, out var list))
            state.Changes[path] = list = new List<GitFileChange>();
        return list;
    }

    public async Task<GitStatus> GetStatusAsync(string localPath, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(localPath) is not var (state, path))
            return GitStatus.Problem(GitStatusKind.FolderNotFound, $"Folder not found: {localPath}");
        lock (_gate)
        {
            var changes = ChangesOf(state, path);
            var isMain = GitRepositoryCatalog.SamePath(path, state.MainPath);
            return new GitStatus
            {
                Kind = GitStatusKind.Repository,
                Branch = state.Worktrees.First(w => w.Path == path).Branch,
                ChangedFiles = changes.Count,
                LinesAdded = changes.Sum(c => c.LinesAdded),
                LinesDeleted = changes.Sum(c => c.LinesDeleted),
                Ahead = isMain ? 0 : 1,
                Behind = 0,
            };
        }
    }

    public async Task<IReadOnlyList<GitWorktree>> GetWorktreesAsync(string localPath, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(localPath) is not var (state, _)) return Array.Empty<GitWorktree>();
        lock (_gate)
        {
            return state.Worktrees
                .Select((w, i) => new GitWorktree { Path = w.Path, Branch = w.Branch, Head = w.Head, IsMain = i == 0 })
                .ToList();
        }
    }

    public Task<GitCommandResult> PullAsync(string localPath, CancellationToken cancellationToken = default) =>
        Simulate("Already up to date.");

    public Task<GitCommandResult> PushAsync(string localPath, CancellationToken cancellationToken = default) =>
        Simulate("Everything up-to-date");

    public async Task<GitChangeSet> GetChangesAsync(string localPath, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(localPath) is not var (state, path))
            return GitChangeSet.Empty with { Error = $"Not a Git repository: {localPath}" };
        lock (_gate)
        {
            var changes = ChangesOf(state, path);
            return new GitChangeSet
            {
                Staged = changes.Where(c => c.IsStaged).ToList(),
                Unstaged = changes.Where(c => !c.IsStaged).ToList(),
            };
        }
    }

    public Task<GitFileDiff> GetFileDiffAsync(string localPath, GitFileChange change, CancellationToken cancellationToken = default)
    {
        if (change.Kind == GitChangeKind.Untracked)
        {
            var lines = Enumerable.Range(1, Math.Max(change.LinesAdded, 1)).Select(i => i switch
            {
                1 => "namespace MyWebApi.Auth;",
                2 => "",
                3 => "public static class RedirectGuard",
                4 => "{",
                5 => "    public static bool IsLocal(string url) =>",
                6 => "        url.StartsWith('/') && !url.StartsWith(\"//\");",
                _ => i == change.LinesAdded ? "}" : "",
            });
            return Task.FromResult(GitDiffParser.ForNewFile(change.Path, string.Join('\n', lines) + "\n"));
        }
        const string diff = """
            @@ -12,8 +12,9 @@ public class LoginController : ControllerBase
                 [HttpGet("callback")]
                 public IActionResult Callback(string returnUrl)
                 {
            -        if (string.IsNullOrEmpty(returnUrl))
            -            return Redirect("/");
            +        if (string.IsNullOrEmpty(returnUrl) || !RedirectGuard.IsLocal(returnUrl))
            +            return Redirect("/home");
            +
                     _logger.LogInformation("Signed in");
                     return Redirect(returnUrl);
                 }
            """;
        return Task.FromResult(GitDiffParser.Parse(change.Path, diff));
    }

    public Task<GitCommandResult> StageAsync(string localPath, IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        UpdateChangesAsync(localPath, list => list.Select(c => paths.Contains(c.Path)
            ? c with { IsStaged = true, Kind = c.Kind == GitChangeKind.Untracked ? GitChangeKind.Added : c.Kind }
            : c).ToList());

    public Task<GitCommandResult> UnstageAsync(string localPath, IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        UpdateChangesAsync(localPath, list => list.Select(c => paths.Contains(c.Path)
            ? c with { IsStaged = false, Kind = c.Kind == GitChangeKind.Added ? GitChangeKind.Untracked : c.Kind }
            : c).ToList());

    public Task<GitCommandResult> DiscardAsync(string localPath, IReadOnlyList<GitFileChange> changes, CancellationToken cancellationToken = default) =>
        UpdateChangesAsync(localPath, list => list.Where(c => c.IsStaged || !changes.Any(d => d.Path == c.Path)).ToList());

    public async Task<GitCommandResult> CommitAsync(string localPath, string message, bool stageAll = false, bool amend = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message)) return Failed("The commit message is required.");
        if (await FindAsync(localPath) is not var (state, path)) return Failed($"Not a Git repository: {localPath}");
        lock (_gate)
        {
            var list = ChangesOf(state, path);
            var committed = list.Count(c => stageAll || c.IsStaged);
            if (committed == 0 && !amend) return Failed("nothing added to commit");
            list.RemoveAll(c => stageAll || c.IsStaged);
            return new GitCommandResult(true, 0, $"[{state.Worktrees.First(w => w.Path == path).Branch} f00ba47] {message.Split('\n')[0]}\n {committed} file(s) changed", string.Empty);
        }
    }

    public async Task<IReadOnlyList<GitBranch>> GetBranchesAsync(string localPath, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(localPath) is not var (state, path)) return Array.Empty<GitBranch>();
        lock (_gate)
        {
            var current = state.Worktrees.First(w => w.Path == path).Branch;
            var local = state.LocalBranches.Order(StringComparer.OrdinalIgnoreCase).Select(name => new GitBranch
            {
                Name = name,
                IsCurrent = name == current,
                CheckedOutIn = name == current ? null : state.Worktrees.FirstOrDefault(w => w.Branch == name).Path,
                Upstream = state.RemoteBranches.Contains($"origin/{name}") ? $"origin/{name}" : null,
                Ahead = state.RemoteBranches.Contains($"origin/{name}") ? 0 : null,
                Behind = state.RemoteBranches.Contains($"origin/{name}") ? 0 : null,
                Tip = "a1b2c3d",
                TipMessage = "Sample commit",
                TipDate = DateTimeOffset.Now.AddHours(-3),
            });
            var remote = state.RemoteBranches.Order(StringComparer.OrdinalIgnoreCase).Select(name => new GitBranch
            {
                Name = name,
                IsRemote = true,
                Tip = "0ff1ce0",
                TipMessage = "Sample remote commit",
                TipDate = DateTimeOffset.Now.AddDays(-1),
            });
            return local.OrderByDescending(b => b.IsCurrent).Concat(remote).ToList();
        }
    }

    public async Task<GitCommandResult> CheckoutAsync(string localPath, GitBranch branch, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(localPath) is not var (state, path)) return Failed($"Not a Git repository: {localPath}");
        lock (_gate)
        {
            var name = branch.LocalName;
            if (state.Worktrees.Any(w => w.Branch == name && w.Path != path))
                return Failed($"fatal: '{name}' is already used by worktree");
            if (!state.LocalBranches.Contains(name)) state.LocalBranches.Add(name);
            SetBranch(state, path, name);
            return new GitCommandResult(true, 0, string.Empty, $"Switched to branch '{name}'");
        }
    }

    public async Task<GitCommandResult> CreateBranchAsync(string localPath, string name, string? startPoint, bool checkout, CancellationToken cancellationToken = default)
    {
        if (GitBranchNames.Validate(name) is { } error) return Failed(error);
        if (await FindAsync(localPath) is not var (state, path)) return Failed($"Not a Git repository: {localPath}");
        lock (_gate)
        {
            if (state.LocalBranches.Contains(name)) return Failed($"fatal: a branch named '{name}' already exists");
            state.LocalBranches.Add(name);
            if (checkout) SetBranch(state, path, name);
            return new GitCommandResult(true, 0, string.Empty, checkout ? $"Switched to a new branch '{name}'" : string.Empty);
        }
    }

    public async Task<GitCommandResult> DeleteBranchAsync(string localPath, string name, bool force = false, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(localPath) is not var (state, _)) return Failed($"Not a Git repository: {localPath}");
        lock (_gate)
        {
            if (state.Worktrees.Any(w => w.Branch == name)) return Failed($"error: cannot delete branch '{name}' used by worktree");
            state.LocalBranches.Remove(name);
            return new GitCommandResult(true, 0, $"Deleted branch {name}.", string.Empty);
        }
    }

    public Task<GitCommandResult> FetchAsync(string localPath, CancellationToken cancellationToken = default) => Simulate(string.Empty);

    public async Task<GitCommandResult> AddWorktreeAsync(string repositoryPath, string worktreePath, string sourceBranch, string? newBranch, CancellationToken cancellationToken = default)
    {
        if (GitWorktreeLocations.ValidateFolder(worktreePath) is { } folderError) return Failed(folderError);
        if (await FindAsync(repositoryPath) is not var (state, _)) return Failed($"Not a Git repository: {repositoryPath}");
        lock (_gate)
        {
            if (state.Worktrees.Any(w => GitRepositoryCatalog.SamePath(w.Path, worktreePath)))
                return Failed($"fatal: '{worktreePath}' already exists");
            var branch = string.IsNullOrWhiteSpace(newBranch) ? new GitBranch { Name = sourceBranch, IsRemote = sourceBranch.StartsWith("origin/") }.LocalName : newBranch.Trim();
            if (!string.IsNullOrWhiteSpace(newBranch) && GitBranchNames.Validate(branch) is { } error) return Failed(error);
            if (state.Worktrees.Any(w => w.Branch == branch)) return Failed($"fatal: '{branch}' is already used by worktree");
            if (!state.LocalBranches.Contains(branch)) state.LocalBranches.Add(branch);
            state.Worktrees.Add((worktreePath.Trim(), branch, "a1b2c3d"));
            return new GitCommandResult(true, 0, $"HEAD is now at a1b2c3d", $"Preparing worktree (new branch '{branch}')");
        }
    }

    public async Task<GitCommandResult> RemoveWorktreeAsync(string repositoryPath, string worktreePath, bool force = false, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(repositoryPath) is not var (state, _)) return Failed($"Not a Git repository: {repositoryPath}");
        lock (_gate)
        {
            var index = state.Worktrees.FindIndex(w => GitRepositoryCatalog.SamePath(w.Path, worktreePath));
            if (index <= 0) return Failed($"fatal: '{worktreePath}' is not a linked working tree");
            if (!force && ChangesOf(state, state.Worktrees[index].Path).Count > 0)
                return Failed($"fatal: '{worktreePath}' contains modified or untracked files, use --force to delete it");
            state.Changes.Remove(state.Worktrees[index].Path);
            state.Worktrees.RemoveAt(index);
            return new GitCommandResult(true, 0, string.Empty, string.Empty);
        }
    }

    public async Task<GitCommandResult> MoveWorktreeAsync(string repositoryPath, string worktreePath, string newPath, CancellationToken cancellationToken = default)
    {
        if (GitWorktreeLocations.ValidateFolder(newPath) is { } error) return Failed(error);
        if (await FindAsync(repositoryPath) is not var (state, _)) return Failed($"Not a Git repository: {repositoryPath}");
        lock (_gate)
        {
            var index = state.Worktrees.FindIndex(w => GitRepositoryCatalog.SamePath(w.Path, worktreePath));
            if (index <= 0) return Failed($"fatal: '{worktreePath}' is not a linked working tree");
            var old = state.Worktrees[index];
            state.Worktrees[index] = (newPath.Trim(), old.Branch, old.Head);
            if (state.Changes.Remove(old.Path, out var changes)) state.Changes[newPath.Trim()] = changes;
            return new GitCommandResult(true, 0, string.Empty, string.Empty);
        }
    }

    private static void SetBranch(RepositoryState state, string path, string branch)
    {
        var index = state.Worktrees.FindIndex(w => w.Path == path);
        state.Worktrees[index] = (path, branch, state.Worktrees[index].Head);
    }

    private async Task<GitCommandResult> UpdateChangesAsync(string localPath, Func<List<GitFileChange>, List<GitFileChange>> update)
    {
        if (await FindAsync(localPath) is not var (state, path)) return Failed($"Not a Git repository: {localPath}");
        lock (_gate)
        {
            state.Changes[path] = update(ChangesOf(state, path));
            return new GitCommandResult(true, 0, string.Empty, string.Empty);
        }
    }

    private static GitCommandResult Failed(string message) => new(false, 1, string.Empty, message);

    private static async Task<GitCommandResult> Simulate(string message)
    {
        await Task.Delay(300);
        return new GitCommandResult(true, 0, message, string.Empty);
    }
}
