using System.Text.Json;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;

namespace DevToolbar.Maui.Services;

/// <summary>
/// Settings service that reads configuration from JSON files.
/// Implements hierarchical config: global.json → template.json → .devtoolbar.json
/// </summary>
public class JsonSettingsService : ISettingsService
{
    private readonly IFileSystemService _fileSystem;
    private readonly string _configDir;
    private List<ProjectConfig> _projects = new();
    private ProjectConfig? _activeProject;

    public event Action<ProjectConfig>? OnActiveProjectChanged;

    public JsonSettingsService(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DevToolbar");

        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        var configFile = Path.Combine(_configDir, "config.json");
        if (_fileSystem.FileExists(configFile))
        {
            var json = _fileSystem.ReadAllTextAsync(configFile).GetAwaiter().GetResult();
            var config = JsonSerializer.Deserialize<AppConfig>(json);
            if (config != null)
            {
                _projects = config.Projects ?? new();
                if (!string.IsNullOrEmpty(config.ActiveProjectId))
                {
                    _activeProject = _projects.FirstOrDefault(p => p.Id == config.ActiveProjectId);
                }
            }
        }

        // Load project-local overrides
        foreach (var project in _projects)
        {
            var localConfig = Path.Combine(project.Path, ".devtoolbar.json");
            if (_fileSystem.FileExists(localConfig))
            {
                var json = _fileSystem.ReadAllTextAsync(localConfig).GetAwaiter().GetResult();
                var local = JsonSerializer.Deserialize<ProjectConfig>(json);
                if (local != null)
                {
                    // Merge local overrides into project config
                    if (local.EnabledPlugins.Count > 0)
                        project.EnabledPlugins = local.EnabledPlugins;
                    if (local.Actions.Count > 0)
                        project.Actions = local.Actions;
                    if (!string.IsNullOrEmpty(local.Theme.AccentColor))
                        project.Theme = local.Theme;
                    if (local.GitRepositories.Count > 0)
                        project.GitRepositories = local.GitRepositories;
                }
            }
        }

        _activeProject ??= _projects.FirstOrDefault();
    }

    public Task<IReadOnlyList<ProjectConfig>> GetProjectsAsync() =>
        Task.FromResult<IReadOnlyList<ProjectConfig>>(_projects.AsReadOnly());

    public ProjectConfig? GetActiveProject() => _activeProject;

    public async Task SetActiveProjectAsync(string projectId)
    {
        _activeProject = _projects.FirstOrDefault(p => p.Id == projectId);
        if (_activeProject != null)
        {
            OnActiveProjectChanged?.Invoke(_activeProject);
            await PersistConfigAsync();
        }
    }

    public async Task SaveProjectAsync(ProjectConfig project)
    {
        var index = _projects.FindIndex(p => p.Id == project.Id);
        if (index >= 0)
        {
            _projects[index] = project;
            await PersistConfigAsync();
        }
    }

    private async Task PersistConfigAsync()
    {
        var configFile = Path.Combine(_configDir, "config.json");
        Directory.CreateDirectory(_configDir);
        var config = new AppConfig
        {
            ActiveProjectId = _activeProject?.Id,
            Projects = _projects
        };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await _fileSystem.WriteAllTextAsync(configFile, json);
    }

    private class AppConfig
    {
        public string? ActiveProjectId { get; set; }
        public List<ProjectConfig>? Projects { get; set; }
    }
}
