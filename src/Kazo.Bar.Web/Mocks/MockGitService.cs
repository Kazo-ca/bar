namespace Kazo.Bar.Web.Mocks;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Plugins.Git;

/// <summary>
/// Simulated Git for the Web test host: no folder is read and no command runs.
/// Every configured repository is a clean working copy on its configured branch.
/// </summary>
public class MockGitService : IGitService
{
    private readonly ISettingsService _settings;

    public MockGitService(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<GitStatus> GetStatusAsync(string localPath, CancellationToken cancellationToken = default)
    {
        foreach (var project in await _settings.GetProjectsAsync())
        {
            var repository = GitRepositoryCatalog.Resolve(project).FirstOrDefault(r => GitRepositoryCatalog.SamePath(r.Path, localPath));
            if (repository != null)
            {
                return new GitStatus
                {
                    Kind = GitStatusKind.Repository,
                    Branch = GitRepositoryCatalog.EffectiveBranch(repository, project),
                    Ahead = 0,
                    Behind = 0,
                };
            }
        }
        foreach (var worktree in await GetWorktreesAsync(localPath, cancellationToken))
        {
            if (!worktree.IsMain && GitRepositoryCatalog.SamePath(worktree.Path, localPath))
                return new GitStatus { Kind = GitStatusKind.Repository, Branch = worktree.Branch, ChangedFiles = 2, Ahead = 1, Behind = 0 };
        }
        return GitStatus.Problem(GitStatusKind.FolderNotFound, $"Folder not found: {localPath}");
    }

    /// <summary>
    /// The first repository of the sample project gets two linked worktrees so the toolbar's
    /// worktree links can be seen and tested (US5.6). The others have only their main tree.
    /// </summary>
    public async Task<IReadOnlyList<GitWorktree>> GetWorktreesAsync(string localPath, CancellationToken cancellationToken = default)
    {
        foreach (var project in await _settings.GetProjectsAsync())
        {
            var repositories = GitRepositoryCatalog.Resolve(project);
            var main = repositories.FirstOrDefault(r => GitRepositoryCatalog.SamePath(r.Path, localPath)
                                                        || _linked.Any(l => GitRepositoryCatalog.SamePath(LinkedPath(r.Path, l.Suffix), localPath)));
            if (main == null) continue;

            var list = new List<GitWorktree>
            {
                new() { Path = main.Path, Branch = GitRepositoryCatalog.EffectiveBranch(main, project), Head = "a1b2c3d", IsMain = true },
            };
            if (main == repositories[0])
            {
                list.AddRange(_linked.Select(l => new GitWorktree { Path = LinkedPath(main.Path, l.Suffix), Branch = l.Branch, Head = l.Head }));
            }
            return list;
        }
        return Array.Empty<GitWorktree>();
    }

    private static readonly (string Suffix, string Branch, string Head)[] _linked =
    [
        ("-hotfix", "hotfix/login-redirect", "9f8e7d6"),
        ("-review", "feature/very-long-branch-name-for-the-review-of-the-new-dashboard", "5c4b3a2"),
    ];

    private static string LinkedPath(string mainPath, string suffix) => mainPath.TrimEnd('/', '\\') + suffix;

    public Task<GitCommandResult> PullAsync(string localPath, CancellationToken cancellationToken = default) =>
        Simulate("Already up to date.");

    public Task<GitCommandResult> PushAsync(string localPath, CancellationToken cancellationToken = default) =>
        Simulate("Everything up-to-date");

    private static async Task<GitCommandResult> Simulate(string message)
    {
        await Task.Delay(300);
        return new GitCommandResult(true, 0, message, string.Empty);
    }
}
