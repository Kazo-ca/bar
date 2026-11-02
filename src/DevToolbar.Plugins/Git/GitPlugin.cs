namespace DevToolbar.Plugins.Git;

using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Plugin showing Git branch status with Quick Sync (Pull/Push) for the active project.
/// A project can declare several repositories; the toolbar shows a selector and
/// Pull/Push run inside the selected repository folder.
/// </summary>
public class GitPlugin : IConfigurablePlugin
{
    private readonly IProcessService _processService;
    private readonly INotificationService _notificationService;

    public string UniqueId => "git-tools";
    public string Name => "Git Tools";
    public string Icon => "git";
    public bool IsEnabled { get; set; } = true;
    public Type SettingsComponentType => typeof(GitSettingsPanel);
    public event Action? OnStateChanged;

    private ProjectConfig _project = new();
    private string? _selectedRepositoryPath;
    private bool _isDirty;
    private string _syncStatus = string.Empty;

    public GitPlugin(IProcessService processService, INotificationService notificationService)
    {
        _processService = processService;
        _notificationService = notificationService;
    }

    /// <summary>Repositories available for the active project.</summary>
    public IReadOnlyList<GitRepository> Repositories => GitRepositoryCatalog.Resolve(_project);

    /// <summary>The repository Pull/Push act on (falls back to the first one).</summary>
    public GitRepository? SelectedRepository =>
        Repositories.FirstOrDefault(r => _selectedRepositoryPath != null && GitRepositoryCatalog.SamePath(r.Path, _selectedRepositoryPath))
        ?? Repositories.FirstOrDefault();

    public Task OnProjectChangedAsync(PluginContext context)
    {
        _project = context.Project;
        _selectedRepositoryPath = Repositories.FirstOrDefault()?.Path;
        _isDirty = false;
        _syncStatus = string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>Selects the repository used by the toolbar buttons.</summary>
    public void SelectRepository(string path)
    {
        _selectedRepositoryPath = path;
        _syncStatus = string.Empty;
        OnStateChanged?.Invoke();
    }

    /// <summary>Branch shown for the selected repository.</summary>
    public string CurrentBranch => SelectedRepository is { } repository
        ? GitRepositoryCatalog.EffectiveBranch(repository, _project)
        : _project.DefaultBranch;

    /// <summary>Whether the working copy has local changes.</summary>
    public bool IsDirty => _isDirty;

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

    /// <summary>Runs <c>git pull</c> inside the selected repository.</summary>
    public Task PullAsync() => RunSyncAsync("pull", "Pull");

    /// <summary>Runs <c>git push</c> inside the selected repository.</summary>
    public Task PushAsync() => RunSyncAsync("push", "Push");

    private async Task RunSyncAsync(string subCommand, string verb)
    {
        var repository = SelectedRepository;
        if (repository == null)
        {
            _notificationService.ShowWarning("No Git repository configured for this project.", "Git Sync");
            return;
        }

        try
        {
            _syncStatus = $"{verb}ing {repository.Name}...";
            OnStateChanged?.Invoke();
            await _processService.StartProcessAsync("git", GitRepositoryCatalog.BuildArguments(repository, subCommand));
            _syncStatus = $"✓ {verb} complete";
            _notificationService.ShowSuccess($"{verb} completed for {repository.Name}", "Git Sync");
        }
        catch (Exception ex)
        {
            _syncStatus = $"✗ {verb} failed: {ex.Message}";
            _notificationService.ShowError($"{verb} failed for {repository.Name}: {ex.Message}", "Git Sync");
        }
        OnStateChanged?.Invoke();
    }
}
