namespace Kazo.Bar.Plugins.Git;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Plugin showing the Git state of the active project's repository, read from its local
/// folder (US5.1), with Quick Sync (Pull/Push). A project can declare several repositories;
/// the toolbar shows a selector and everything acts on the selected repository. When the
/// repository has linked worktrees, one of them can be selected instead (US5.6): status and
/// Pull/Push then run in that worktree's folder. The repository, branch and worktree are shown
/// in the project pill (<see cref="IProjectPillContent"/>); the Git module itself only holds
/// Pull, Push and Pull &amp; Push, and stays glued to the pill.
/// </summary>
public class GitPlugin : IConfigurablePlugin, IProjectPillContent
{
    private readonly IGitService _gitService;
    private readonly INotificationService _notificationService;

    public string UniqueId => "git-tools";
    public string Name => "Git Tools";
    public string Icon => "git";
    public bool IsEnabled { get; set; } = true;
    public Type SettingsComponentType => typeof(GitSettingsPanel);
    public event Action? OnStateChanged;

    /// <summary>
    /// The Git module only holds its buttons; long repository or branch names are in the
    /// project pill, which is the one the user can widen (US3.6).
    /// </summary>
    public PluginLayout Layout => PluginLayout.Fixed;

    private ProjectConfig _project = new();
    private string? _selectedRepositoryPath;
    private string? _selectedWorktreePath;
    private Dictionary<string, IReadOnlyList<GitWorktree>> _worktrees = new(StringComparer.OrdinalIgnoreCase);
    private GitStatus? _status;
    private string _syncStatus = string.Empty;
    private int _refreshing;
    private bool _isSyncing;

    public GitPlugin(IGitService gitService, INotificationService notificationService)
    {
        _gitService = gitService;
        _notificationService = notificationService;
    }

    /// <summary>Repositories available for the active project.</summary>
    public IReadOnlyList<GitRepository> Repositories => GitRepositoryCatalog.Resolve(_project);

    /// <summary>The repository Pull/Push act on (falls back to the first one).</summary>
    public GitRepository? SelectedRepository =>
        Repositories.FirstOrDefault(r => _selectedRepositoryPath != null && GitRepositoryCatalog.SamePath(r.Path, _selectedRepositoryPath))
        ?? Repositories.FirstOrDefault();

    public async Task OnProjectChangedAsync(PluginContext context)
    {
        _project = context.Project;
        _selectedRepositoryPath = Repositories.FirstOrDefault()?.Path;
        _selectedWorktreePath = null;
        _worktrees = new(StringComparer.OrdinalIgnoreCase);
        _status = null;
        _syncStatus = string.Empty;
        await Task.WhenAll(RefreshStatusAsync(), LoadWorktreesAsync());
    }

    /// <summary>Selects the repository used by the toolbar buttons and reads its state.</summary>
    public Task SelectRepositoryAsync(string path) => SelectWorktreeAsync(path, null);

    /// <summary>
    /// Selects a repository and one of its worktrees (null or the main one: the repository folder)
    /// and reads its state (US5.6).
    /// </summary>
    public async Task SelectWorktreeAsync(string repositoryPath, string? worktreePath)
    {
        _selectedRepositoryPath = repositoryPath;
        _selectedWorktreePath = worktreePath != null && !GitRepositoryCatalog.SamePath(worktreePath, repositoryPath)
            ? worktreePath
            : null;
        _status = null;
        _syncStatus = string.Empty;
        OnStateChanged?.Invoke();
        await RefreshStatusAsync();
    }

    /// <summary>
    /// Reads the selected repository's state from its folder. Calls made while a refresh
    /// is running are skipped (the toolbar refreshes periodically).
    /// </summary>
    public async Task RefreshStatusAsync()
    {
        var path = ActivePath;
        if (path == null)
        {
            _status = null;
            OnStateChanged?.Invoke();
            return;
        }
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
            return;
        try
        {
            var status = await _gitService.GetStatusAsync(path);
            // Ignore a late answer for a folder that is no longer selected.
            if (ActivePath is { } current && GitRepositoryCatalog.SamePath(current, path))
            {
                _status = status;
                OnStateChanged?.Invoke();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    /// <summary>
    /// Reads the worktree list of every repository of the project (shown in the selector menu).
    /// A repository whose list cannot be read simply shows no worktree.
    /// </summary>
    public async Task LoadWorktreesAsync()
    {
        var repositories = Repositories;
        var lists = await Task.WhenAll(repositories.Select(async r =>
        {
            try
            {
                return (r.Path, List: await _gitService.GetWorktreesAsync(r.Path));
            }
            catch (Exception)
            {
                return (r.Path, List: (IReadOnlyList<GitWorktree>)Array.Empty<GitWorktree>());
            }
        }));
        var map = new Dictionary<string, IReadOnlyList<GitWorktree>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, list) in lists)
            map[path] = list;
        _worktrees = map;
        OnStateChanged?.Invoke();
    }

    /// <summary>Worktrees of a repository (main one first); empty when it has none or they are not read yet.</summary>
    public IReadOnlyList<GitWorktree> WorktreesOf(GitRepository repository) =>
        _worktrees.TryGetValue(repository.Path, out var list) ? list : Array.Empty<GitWorktree>();

    /// <summary>True when the repository has at least one linked worktree besides its main one.</summary>
    public bool HasWorktrees(GitRepository repository) => WorktreesOf(repository).Count > 1;

    /// <summary>Folder where status, Pull and Push run: the selected worktree, else the selected repository.</summary>
    public string? ActivePath => _selectedWorktreePath ?? SelectedRepository?.Path;

    /// <summary>
    /// The worktree the toolbar works in, when the selected repository has worktrees; null otherwise.
    /// </summary>
    public GitWorktree? CurrentWorktree =>
        SelectedRepository is { } repository && HasWorktrees(repository) && ActivePath is { } path
            ? WorktreesOf(repository).FirstOrDefault(w => GitRepositoryCatalog.SamePath(w.Path, path))
            : null;

    /// <summary>Whether this worktree is the one the toolbar works in.</summary>
    public bool IsActive(GitRepository repository, GitWorktree worktree) =>
        SelectedRepository is { } selected && GitRepositoryCatalog.SamePath(selected.Path, repository.Path)
        && ActivePath is { } path && GitRepositoryCatalog.SamePath(worktree.Path, path);

    /// <summary>Last state read from the selected repository's folder; null before the first read.</summary>
    public GitStatus? Status => _status;

    /// <summary>
    /// Branch shown for the selected repository: the one checked out in the folder, or the
    /// configured branch until the folder has been read (or when it cannot be read).
    /// </summary>
    public string CurrentBranch => _status is { IsRepository: true, Branch: { } branch }
        ? branch
        : CurrentWorktree is { IsMain: false } worktree
            ? worktree.Branch ?? worktree.Head ?? string.Empty
        : SelectedRepository is { } repository
            ? GitRepositoryCatalog.EffectiveBranch(repository, _project)
            : _project.DefaultBranch;

    /// <summary>Whether the working copy has local changes.</summary>
    public bool IsDirty => _status?.IsDirty ?? false;

    /// <summary>True while Pull or Push runs.</summary>
    public bool IsSyncing => _isSyncing;

    /// <summary>Last Pull/Push status message.</summary>
    public string SyncStatus => _syncStatus;

    /// <summary>
    /// Renders <see cref="GitToolbarView"/>, which subscribes to <see cref="OnStateChanged"/>
    /// so the Git module refreshes itself whatever the hosting layout does.
    /// </summary>
    public RenderFragment? Render() => builder =>
    {
        builder.OpenComponent<GitToolbarView>(0);
        builder.AddComponentParameter(1, nameof(GitToolbarView.Plugin), this);
        builder.CloseComponent();
    };

    /// <summary>Repository, state, branch and worktree, shown in the project pill.</summary>
    public RenderFragment? RenderPillInfo() => builder =>
    {
        builder.OpenComponent<GitPillInfo>(0);
        builder.AddComponentParameter(1, nameof(GitPillInfo.Plugin), this);
        builder.CloseComponent();
    };

    /// <summary>Repositories and worktrees, listed in the project pill's menu.</summary>
    public RenderFragment? RenderPillMenu(Func<Task> closeMenu) => builder =>
    {
        builder.OpenComponent<GitRepositoryMenu>(0);
        builder.AddComponentParameter(1, nameof(GitRepositoryMenu.Plugin), this);
        builder.AddComponentParameter(2, nameof(GitRepositoryMenu.CloseMenu), closeMenu);
        builder.CloseComponent();
    };

    /// <summary>Worktrees may have been added or removed since the last read.</summary>
    public Task OnPillMenuOpeningAsync() => LoadWorktreesAsync();

    public string? PillTooltip => SelectedRepository is not { } repository
        ? "No Git repository configured for this project"
        : CurrentWorktree is { } worktree
            ? $"{repository.Name} — worktree {worktree.Name} ({worktree.Path})"
            : $"{repository.Name} ({repository.Path})";

    /// <summary>Runs <c>git pull</c> inside the selected repository and shows its real result.</summary>
    public Task PullAsync() => RunSyncAsync(_gitService.PullAsync, "Pull");

    /// <summary>Runs <c>git push</c> inside the selected repository and shows its real result.</summary>
    public Task PushAsync() => RunSyncAsync(_gitService.PushAsync, "Push");

    /// <summary>Pull then Push in one click; Push is skipped when Pull fails or leaves conflicts.</summary>
    public Task PullAndPushAsync() => RunSyncAsync(PullThenPushAsync, "Sync");

    private async Task<GitCommandResult> PullThenPushAsync(string path, CancellationToken cancellationToken)
    {
        var pull = await _gitService.PullAsync(path, cancellationToken);
        if (!pull.Success || pull.HasConflicts) return pull;
        return await _gitService.PushAsync(path, cancellationToken);
    }

    private async Task RunSyncAsync(Func<string, CancellationToken, Task<GitCommandResult>> command, string verb)
    {
        var repository = SelectedRepository;
        var path = ActivePath;
        if (repository == null || path == null)
        {
            _notificationService.ShowWarning("No Git repository configured for this project.", "Git Sync");
            return;
        }
        if (_isSyncing)
            return;

        var target = CurrentWorktree is { IsMain: false } worktree ? $"{repository.Name} ({worktree.Name})" : repository.Name;
        _isSyncing = true;
        _syncStatus = $"{verb}ing {target}...";
        OnStateChanged?.Invoke();
        try
        {
            var result = await command(path, CancellationToken.None);
            ReportSyncResult(result, verb, target);
        }
        catch (Exception ex)
        {
            _syncStatus = $"✗ {verb} failed: {ex.Message}";
            _notificationService.ShowError($"{verb} failed for {target}: {ex.Message}", "Git Sync");
        }
        finally
        {
            _isSyncing = false;
        }
        OnStateChanged?.Invoke();
        await RefreshStatusAsync();
    }

    private void ReportSyncResult(GitCommandResult result, string verb, string repositoryName)
    {
        if (result.HasConflicts)
        {
            _syncStatus = $"⚠ {verb}: conflicts to resolve";
            _notificationService.ShowWarning($"{verb} on {repositoryName} left conflicts to resolve. {result.Summary}", "Git Sync");
        }
        else if (result.Success)
        {
            _syncStatus = $"✓ {verb} complete";
            var detail = string.IsNullOrEmpty(result.Summary) ? string.Empty : $" {result.Summary}";
            _notificationService.ShowSuccess($"{verb} completed for {repositoryName}.{detail}", "Git Sync");
        }
        else
        {
            _syncStatus = $"✗ {verb} failed: {result.Summary}";
            _notificationService.ShowError($"{verb} failed for {repositoryName}: {result.Summary}", "Git Sync");
        }
    }
}
