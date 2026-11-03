namespace DevToolbar.Core.Interfaces;

using DevToolbar.Core.Models;

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
    /// Where the configuration lives, shown in Settings (for example the path of
    /// <c>config.json</c>). Null when the host keeps it in memory.
    /// </summary>
    string? ConfigurationLocation => null;

    /// <summary>Event fired when the active project changes.</summary>
    event Action<ProjectConfig>? OnActiveProjectChanged;
}
