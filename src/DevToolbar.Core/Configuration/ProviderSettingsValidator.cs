namespace DevToolbar.Core.Configuration;

using System.Text.RegularExpressions;
using DevToolbar.Core.Models;

/// <summary>
/// Checks provider settings before they are saved, and builds default secret references.
/// The names end up in API URLs, so only the characters each service allows are accepted.
/// </summary>
public static partial class ProviderSettingsValidator
{
    /// <summary>Returns a user-facing error, or null when valid.</summary>
    public static string? Validate(WorkItemSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var error = source.Provider switch
        {
            WorkItemProviderKind.AzureDevOps when !AzureOrganization().IsMatch(source.Organization) =>
                "The Azure DevOps organization uses letters, digits and '-' (dev.azure.com/<organization>).",
            WorkItemProviderKind.AzureDevOps when string.IsNullOrWhiteSpace(source.Project) || source.Project.Length > 64 =>
                "The Azure DevOps project is required (64 characters at most).",
            WorkItemProviderKind.GitHub when !IsGitHubRepository(source.Repository) =>
                "The GitHub repository is written owner/name.",
            _ => null,
        };
        return error ?? ValidateSecretRef(source.SecretRef);
    }

    /// <summary>Returns a user-facing error, or null when valid.</summary>
    public static string? Validate(GitHubTracking tracking)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        return IsGitHubRepository(tracking.Repository)
            ? ValidateSecretRef(tracking.SecretRef)
            : "The GitHub repository is written owner/name.";
    }

    public static bool IsGitHubRepository(string? value) => value != null && GitHubRepository().IsMatch(value);

    /// <summary>
    /// Default reference for a repository's token on a service, for example <c>alpha-api-github</c>.
    /// The same default is used by work items and Actions so one GitHub token serves both.
    /// </summary>
    public static string DefaultSecretRef(ProjectConfig project, GitRepository repository, string service)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(repository);
        return ProjectFactory.UniqueId($"{project.Id} {repository.Name} {service}", []);
    }

    private static string? ValidateSecretRef(string secretRef) =>
        SecretRef().IsMatch(secretRef) ? null : "The token reference uses letters, digits, '.', '-' or '_'.";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]{0,49}$")]
    private static partial Regex AzureOrganization();

    [GeneratedRegex("^[A-Za-z0-9-]{1,39}/[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex GitHubRepository();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex SecretRef();
}
