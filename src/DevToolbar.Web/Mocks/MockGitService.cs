namespace DevToolbar.Web.Mocks;

using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using DevToolbar.Plugins.Git;

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
        return GitStatus.Problem(GitStatusKind.FolderNotFound, $"Folder not found: {localPath}");
    }

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
