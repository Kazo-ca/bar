namespace Kazo.Bar.Plugins.Git;

using Kazo.Bar.Core.Configuration;
using Kazo.Bar.Core.Models;

/// <summary>
/// Pure logic for the Git plugin's repository list: resolving, validating,
/// adding and removing repositories. Always returns new lists (no mutation).
/// </summary>
public static class GitRepositoryCatalog
{
    private const int MaxNameLength = 60;

    /// <summary>
    /// Repositories the plugin works with: the configured list, or an implicit
    /// repository at the project path when none are configured.
    /// </summary>
    public static IReadOnlyList<GitRepository> Resolve(ProjectConfig project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.GitRepositories.Count > 0)
        {
            return project.GitRepositories.AsReadOnly();
        }

        if (string.IsNullOrWhiteSpace(project.Path))
        {
            return Array.Empty<GitRepository>();
        }

        return new[]
        {
            new GitRepository
            {
                Name = string.IsNullOrWhiteSpace(project.Name) ? NameFromPath(project.Path) : project.Name,
                Path = project.Path,
                RemoteUrl = project.RepositoryUrl,
                DefaultBranch = project.DefaultBranch,
            },
        };
    }

    /// <summary>Branch to display for a repository (its own, else the project's).</summary>
    public static string EffectiveBranch(GitRepository repository, ProjectConfig project) =>
        string.IsNullOrWhiteSpace(repository.DefaultBranch) ? project.DefaultBranch : repository.DefaultBranch;

    /// <summary>
    /// Trims the candidate and fills a default name from the folder.
    /// </summary>
    public static GitRepository Normalize(GitRepository candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var path = candidate.Path.Trim();
        var name = candidate.Name.Trim();
        return candidate with
        {
            Path = path,
            Name = string.IsNullOrEmpty(name) ? NameFromPath(path) : name,
            RemoteUrl = candidate.RemoteUrl.Trim(),
            DefaultBranch = candidate.DefaultBranch.Trim(),
        };
    }

    /// <summary>
    /// Validates a normalized candidate against the existing list.
    /// Returns a user-facing error message, or null when valid.
    /// </summary>
    public static string? Validate(IReadOnlyList<GitRepository> existing, GitRepository candidate)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(candidate);

        if (string.IsNullOrWhiteSpace(candidate.Path))
            return "The local folder is required.";
        if (candidate.Path.IndexOfAny(RepositoryPaths.ForbiddenChars) >= 0)
            return "The local folder contains invalid characters.";
        if (candidate.Name.Length > MaxNameLength)
            return $"The name must be {MaxNameLength} characters or fewer.";
        if (existing.Any(r => SamePath(r.Path, candidate.Path)))
            return "This folder is already in the list.";
        if (existing.Any(r => string.Equals(r.Name, candidate.Name, StringComparison.OrdinalIgnoreCase)))
            return "Another repository already uses this name.";
        if (!string.IsNullOrEmpty(candidate.RemoteUrl) && !IsValidRemote(candidate.RemoteUrl))
            return "The remote URL must be an https, ssh or git@host:path address.";
        if (candidate.DefaultBranch.Any(char.IsWhiteSpace))
            return "The branch name cannot contain spaces.";
        return null;
    }

    /// <summary>Returns a new list with the candidate appended. Throws when invalid.</summary>
    public static List<GitRepository> Add(IReadOnlyList<GitRepository> existing, GitRepository candidate)
    {
        var normalized = Normalize(candidate);
        var error = Validate(existing, normalized);
        if (error != null)
            throw new ArgumentException(error, nameof(candidate));
        return [.. existing, normalized];
    }

    /// <summary>Returns a new list without the repository at <paramref name="path"/>.</summary>
    public static List<GitRepository> Remove(IReadOnlyList<GitRepository> existing, string path) =>
        existing.Where(r => !SamePath(r.Path, path)).ToList();

    /// <summary>Returns a new list with the repository at <paramref name="index"/> moved by <paramref name="offset"/>.</summary>
    public static List<GitRepository> Move(IReadOnlyList<GitRepository> existing, int index, int offset)
    {
        var target = index + offset;
        var list = existing.ToList();
        if (index < 0 || index >= list.Count || target < 0 || target >= list.Count)
            return list;
        (list[index], list[target]) = (list[target], list[index]);
        return list;
    }

    /// <summary>Case-insensitive path comparison that ignores trailing separators.</summary>
    public static bool SamePath(string a, string b) => RepositoryPaths.SamePath(a, b);

    private static string NameFromPath(string path) => RepositoryPaths.NameFromPath(path);

    private static bool IsValidRemote(string url)
    {
        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
            return url.Contains(':') && !url.Any(char.IsWhiteSpace);

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == "ssh");
    }
}
