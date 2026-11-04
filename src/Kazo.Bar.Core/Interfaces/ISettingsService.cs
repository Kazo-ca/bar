namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>
/// Projects of the user configuration and the active project.
/// </summary>
public interface ISettingsService
{
    /// <summary>Get the list of configured projects.</summary>
    Task<IReadOnlyList<ProjectConfig>> GetProjectsAsync();

    /// <summary>Get the currently active project.</summary>
    ProjectConfig? GetActiveProject();

    /// <summary>Set the active project by ID.</summary>
    Task SetActiveProjectAsync(string projectId);

    /// <summary>Save changes to a project configuration (US8.3).</summary>
    Task SaveProjectAsync(ProjectConfig project);

    /// <summary>
    /// Adds a new project (see <c>ProjectFactory</c>) and makes it the active project.
    /// </summary>
    /// <exception cref="ArgumentException">A project with the same id already exists.</exception>
    Task AddProjectAsync(ProjectConfig project) =>
        throw new NotSupportedException("This host cannot create projects.");

    /// <summary>
    /// Where the configuration lives, shown in Settings (for example the path of
    /// <c>config.json</c>). Null when the host keeps it in memory.
    /// </summary>
    string? ConfigurationLocation => null;

    /// <summary>Event fired when the active project changes.</summary>
    event Action<ProjectConfig>? OnActiveProjectChanged;
}
