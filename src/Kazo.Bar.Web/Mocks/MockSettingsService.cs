namespace Kazo.Bar.Web.Mocks;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>
/// Mock settings service with sample project data for web testing.
/// </summary>
public class MockSettingsService : ISettingsService
{
    private readonly List<ProjectConfig> _projects;
    private ProjectConfig? _activeProject;

    public event Action<ProjectConfig>? OnActiveProjectChanged;

    /// <summary>The Web host keeps its sample projects in memory; this mirrors the Windows path.</summary>
    public string? ConfigurationLocation => @"%APPDATA%\Kazo\Bar\config.json (simulated)";

    public MockSettingsService()
    {
        _projects = new List<ProjectConfig>
        {
            new ProjectConfig
            {
                Id = "proj-webapi",
                Name = "MyWebAPI",
                Path = "/projects/my-webapi",
                ProjectType = "WebApi",
                DefaultBranch = "develop",
                RepositoryUrl = "https://github.com/example/my-webapi",
                Theme = new ThemeConfig { AccentColor = "#0078D7" },
                EnabledPlugins = new List<string> { "git-tools", "work-items", "time-tracker", "github-agents" },
                GitRepositories = new List<GitRepository>
                {
                    new GitRepository { Name = "my-webapi", Path = "/projects/my-webapi", RemoteUrl = "https://github.com/example/my-webapi" },
                    new GitRepository { Name = "shared-lib", Path = "/projects/shared-lib", RemoteUrl = "https://github.com/example/shared-lib", DefaultBranch = "main" }
                },
                Actions = new List<ActionConfig>
                {
                    new ActionConfig { Label = "Visual Studio", Icon = "🟣", ProcessPath = "devenv.exe", WindowTitleRegex = ".*Visual Studio.*" },
                    new ActionConfig { Label = "Postman", Icon = "🟠", ProcessPath = "postman.exe" },
                    new ActionConfig { Label = "Terminal", Icon = "⬛", ProcessPath = "wt.exe" },
                    new ActionConfig { Label = "Build Script", Icon = "🔨", ProcessPath = "build.ps1", ActionType = ActionType.Script, Interpreter = "pwsh" }
                }
            },
            new ProjectConfig
            {
                Id = "proj-frontend",
                Name = "FrontEnd App",
                Path = "/projects/frontend",
                ProjectType = "SPA",
                DefaultBranch = "main",
                RepositoryUrl = "https://github.com/example/frontend-app",
                Theme = new ThemeConfig { Name = ThemeNames.Green, AccentColor = "#4CAF50" },
                EnabledPlugins = new List<string> { "git-tools", "work-items", "time-tracker" },
                Actions = new List<ActionConfig>
                {
                    new ActionConfig { Label = "VS Code", Icon = "🔵", ProcessPath = "code.exe" },
                    new ActionConfig { Label = "Browser", Icon = "🌐", ProcessPath = "chrome.exe", WindowTitleRegex = ".*localhost.*" },
                    new ActionConfig { Label = "npm start", Icon = "📦", ProcessPath = "start.sh", ActionType = ActionType.Script, Interpreter = "bash" }
                }
            },
            new ProjectConfig
            {
                Id = "proj-devops",
                Name = "DevOps Pipeline",
                Path = "/projects/devops",
                ProjectType = "Infrastructure",
                DefaultBranch = "release/1.0",
                RepositoryUrl = "https://dev.azure.com/example/devops-pipeline",
                Theme = new ThemeConfig
                {
                    Name = ThemeNames.Dark,
                    AccentColor = "#E53935",
                    // US3.2: custom CSS from the project config is injected into the toolbar
                    CustomCss = ".toolbar-shell { --devops-custom-css: applied; }"
                },
                EnabledPlugins = new List<string> { "git-tools", "github-agents" },
                Actions = new List<ActionConfig>
                {
                    new ActionConfig { Label = "Azure Portal", Icon = "☁️", ProcessPath = "https://portal.azure.com", WindowTitleRegex = ".*Azure.*" },
                    // US4.1: invalid path to demonstrate the error toast
                    new ActionConfig { Label = "Legacy Tool", Icon = "🧰", ProcessPath = "C:/missing/legacy-tool.exe" }
                }
            }
        };

        _activeProject = _projects[0];
    }

    public Task<IReadOnlyList<ProjectConfig>> GetProjectsAsync() =>
        Task.FromResult<IReadOnlyList<ProjectConfig>>(_projects.AsReadOnly());

    public ProjectConfig? GetActiveProject() => _activeProject;

    public Task SetActiveProjectAsync(string projectId)
    {
        _activeProject = _projects.FirstOrDefault(p => p.Id == projectId);
        if (_activeProject != null)
        {
            OnActiveProjectChanged?.Invoke(_activeProject);
        }
        return Task.CompletedTask;
    }

    public Task AddProjectAsync(ProjectConfig project)
    {
        if (_projects.Any(p => string.Equals(p.Id, project.Id, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A project with id '{project.Id}' already exists.", nameof(project));
        _projects.Add(project);
        _activeProject = project;
        OnActiveProjectChanged?.Invoke(project);
        return Task.CompletedTask;
    }

    public Task RemoveProjectAsync(string projectId)
    {
        var project = _projects.FirstOrDefault(p => p.Id == projectId);
        if (project == null)
            return Task.CompletedTask;
        if (_projects.Count <= 1)
            throw new InvalidOperationException("The last project cannot be removed.");

        _projects.Remove(project);
        if (_activeProject?.Id == project.Id)
        {
            _activeProject = _projects[0];
            OnActiveProjectChanged?.Invoke(_activeProject);
        }
        return Task.CompletedTask;
    }

    public Task SaveProjectAsync(ProjectConfig project)
    {
        var index = _projects.FindIndex(p => p.Id == project.Id);
        if (index >= 0)
        {
            _projects[index] = project;
            if (_activeProject?.Id == project.Id)
                _activeProject = project;
        }
        return Task.CompletedTask;
    }
}
