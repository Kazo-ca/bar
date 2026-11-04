namespace Kazo.Bar.Infrastructure.Configuration;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>
/// <see cref="ISettingsService"/> over the single user configuration file.
/// Projects come only from <c>config.json</c>: <c>.devtoolbar.json</c> overrides in project folders are no longer read (US2.3).
/// </summary>
public sealed class UserConfigSettingsService : ISettingsService
{
    private readonly UserConfigurationStore _store;

    public UserConfigSettingsService(UserConfigurationStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public event Action<ProjectConfig>? OnActiveProjectChanged;

    public string? ConfigurationLocation => _store.FilePath;

    public Task<IReadOnlyList<ProjectConfig>> GetProjectsAsync() =>
        Task.FromResult<IReadOnlyList<ProjectConfig>>(_store.Current.Projects.ToList());

    public ProjectConfig? GetActiveProject()
    {
        var projects = _store.Current.Projects;
        return projects.FirstOrDefault(p => p.Id == _store.Current.ActiveProjectId) ?? projects.FirstOrDefault();
    }

    public async Task SetActiveProjectAsync(string projectId)
    {
        var project = _store.Current.Projects.FirstOrDefault(p => p.Id == projectId);
        if (project == null)
            return;
        await _store.UpdateAsync(c => c.ActiveProjectId = project.Id);
        OnActiveProjectChanged?.Invoke(project);
    }

    public async Task AddProjectAsync(ProjectConfig project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Id);
        if (_store.Current.Projects.Any(p => string.Equals(p.Id, project.Id, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A project with id '{project.Id}' already exists.", nameof(project));

        await _store.UpdateAsync(c =>
        {
            c.Projects = [.. c.Projects, project];
            c.ActiveProjectId = project.Id;
        });
        OnActiveProjectChanged?.Invoke(project);
    }

    public Task SaveProjectAsync(ProjectConfig project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!_store.Current.Projects.Any(p => p.Id == project.Id))
            return Task.CompletedTask;
        return _store.UpdateAsync(c =>
            c.Projects = c.Projects.Select(p => p.Id == project.Id ? project : p).ToList());
    }
}
