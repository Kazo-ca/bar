namespace Kazo.Bar.Core.Configuration;

using System.Text;
using Kazo.Bar.Core.Models;

/// <summary>
/// Creates a new project linked to one repository and its local folder (US5.1, main case:
/// one project = one repository). Pure logic: validation and a unique id, no I/O.
/// </summary>
public static class ProjectFactory
{
    public const int MaxNameLength = 60;

    /// <summary>Returns a user-facing error, or null when the input can create a project.</summary>
    public static string? Validate(string name, string localPath)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        var trimmedPath = (localPath ?? string.Empty).Trim();
        if (trimmedPath.Length == 0)
            return "The local folder is required.";
        if (trimmedPath.IndexOfAny(RepositoryPaths.ForbiddenChars) >= 0)
            return "The local folder contains invalid characters.";
        var effectiveName = trimmedName.Length == 0 ? RepositoryPaths.NameFromPath(trimmedPath) : trimmedName;
        if (effectiveName.Length == 0)
            return "The project name is required.";
        if (effectiveName.Length > MaxNameLength)
            return $"The name must be {MaxNameLength} characters or fewer.";
        return null;
    }

    /// <summary>
    /// Builds the project: name (folder name by default), a unique id, every available plugin
    /// enabled and one repository at <paramref name="localPath"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The input is invalid (see <see cref="Validate"/>).</exception>
    public static ProjectConfig Create(string name, string localPath, IEnumerable<string> existingIds, IEnumerable<string> pluginIds)
    {
        ArgumentNullException.ThrowIfNull(existingIds);
        ArgumentNullException.ThrowIfNull(pluginIds);
        var error = Validate(name, localPath);
        if (error != null)
            throw new ArgumentException(error, nameof(name));

        var path = localPath.Trim();
        var projectName = string.IsNullOrWhiteSpace(name) ? RepositoryPaths.NameFromPath(path) : name.Trim();
        return new ProjectConfig
        {
            Id = UniqueId(projectName, existingIds),
            Name = projectName,
            Path = path,
            EnabledPlugins = pluginIds.Distinct().ToList(),
            GitRepositories =
            [
                new GitRepository { Name = RepositoryPaths.NameFromPath(path), Path = path },
            ],
        };
    }

    /// <summary>Lower-case slug of the name, suffixed with -2, -3... when already used.</summary>
    public static string UniqueId(string name, IEnumerable<string> existingIds)
    {
        var used = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
        var slug = Slug(name);
        var candidate = slug;
        for (var i = 2; used.Contains(candidate); i++)
            candidate = $"{slug}-{i}";
        return candidate;
    }

    private static string Slug(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
            else if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark
                     && builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }
        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? "project" : slug;
    }
}
