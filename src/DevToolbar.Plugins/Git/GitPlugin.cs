namespace DevToolbar.Plugins.Git;

using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Plugin showing the Git state of the active project's repository, read from its local
/// folder (US5.1), with Quick Sync (Pull/Push). A project can declare several repositories;
/// the toolbar shows a selector and everything acts on the selected repository.
/// </summary>
public class GitPlugin : IConfigurablePlugin
{
    private readonly IGitService _gitService;
    private readonly INotificationService _notificationService;

    public string UniqueId => "git-tools";
    public string Name => "Git Tools";
    public string Icon => "git";
    public bool IsEnabled { get; set; } = true;
    public Type SettingsComponentType => typeof(GitSettingsPanel);
    public event Action? OnStateChanged;

    private ProjectConfig _project = new();
    private string? _selectedRepositoryPath;
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
        _status = null;
        _syncStatus = string.Empty;
        await RefreshStatusAsync();
    }

    /// <summary>Selects the repository used by the toolbar buttons and reads its state.</summary>
    public async Task SelectRepositoryAsync(string path)
    {
        _selectedRepositoryPath = path;
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
        var repository = SelectedRepository;
        if (repository == null)
        {
            _status = null;
            OnStateChanged?.Invoke();
            return;
        }
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
            return;
        try
        {
            var status = await _gitService.GetStatusAsync(repository.Path);
            // Ignore a late answer for a repository that is no longer selected.
            if (SelectedRepository is { } current && GitRepositoryCatalog.SamePath(current.Path, repository.Path))
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

    /// <summary>Last state read from the selected repository's folder; null before the first read.</summary>
    public GitStatus? Status => _status;

    /// <summary>
    /// Branch shown for the selected repository: the one checked out in the folder, or the
    /// configured branch until the folder has been read (or when it cannot be read).
    /// </summary>
    public string CurrentBranch => _status is { IsRepository: true, Branch: { } branch }
        ? branch
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

    /// <summary>Runs <c>git pull</c> inside the selected repository and shows its real result.</summary>
    public Task PullAsync() => RunSyncAsync(_gitService.PullAsync, "Pull");

    /// <summary>Runs <c>git push</c> inside the selected repository and shows its real result.</summary>
    public Task PushAsync() => RunSyncAsync(_gitService.PushAsync, "Push");

    private async Task RunSyncAsync(Func<string, CancellationToken, Task<GitCommandResult>> command, string verb)
    {
        var repository = SelectedRepository;
        if (repository == null)
        {
            _notificationService.ShowWarning("No Git repository configured for this project.", "Git Sync");
            return;
        }
        if (_isSyncing)
            return;

        _isSyncing = true;
        _syncStatus = $"{verb}ing {repository.Name}...";
        OnStateChanged?.Invoke();
        try
        {
            var result = await command(repository.Path, CancellationToken.None);
            ReportSyncResult(result, verb, repository.Name);
        }
        catch (Exception ex)
        {
            _syncStatus = $"✗ {verb} failed: {ex.Message}";
            _notificationService.ShowError($"{verb} failed for {repository.Name}: {ex.Message}", "Git Sync");
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
