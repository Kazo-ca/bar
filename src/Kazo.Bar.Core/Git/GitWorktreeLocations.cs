namespace Kazo.Bar.Core.Git;

using Kazo.Bar.Core.Configuration;
using Kazo.Bar.Core.Models;

/// <summary>
/// Where new worktrees of a repository are created (US5.8): the repository's configured
/// worktree folder, else a <c>&lt;repository&gt;.worktrees</c> folder next to the repository.
/// Each worktree gets a sub-folder named after its branch.
/// </summary>
public static class GitWorktreeLocations
{
    /// <summary>Suffix of the default worktree folder (<c>C:\src\api</c> gives <c>C:\src\api.worktrees</c>).</summary>
    public const string DefaultSuffix = ".worktrees";

    /// <summary>Folder that holds the repository's worktrees.</summary>
    public static string RootFor(GitRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!string.IsNullOrWhiteSpace(repository.WorktreesFolder))
            return repository.WorktreesFolder.Trim();
        return DefaultRootFor(repository.Path);
    }

    /// <summary>Default worktree folder: next to the repository, named after it.</summary>
    public static string DefaultRootFor(string repositoryPath)
    {
        var trimmed = repositoryPath.Trim().TrimEnd('/', '\\');
        return trimmed + DefaultSuffix;
    }

    /// <summary>
    /// Folder suggested for a worktree of <paramref name="branch"/>: the branch name with
    /// <c>/</c> turned into <c>-</c> (<c>feature/login</c> gives <c>feature-login</c>).
    /// </summary>
    public static string SuggestPath(GitRepository repository, string branch)
    {
        var root = RootFor(repository);
        var separator = root.Contains('\\') && !root.Contains('/') ? '\\' : '/';
        return root.TrimEnd('/', '\\') + separator + FolderName(branch);
    }

    /// <summary>A folder name made from a branch name.</summary>
    public static string FolderName(string branch)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var chars = branch.Trim().Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        var name = new string(chars).Trim('-', '.', ' ');
        return name.Length == 0 ? "worktree" : name;
    }

    /// <summary>Returns a user-facing error for a worktree folder, or null when it can be used.</summary>
    public static string? ValidateFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return "The folder is required.";
        if (folder.IndexOfAny(RepositoryPaths.ForbiddenChars) >= 0) return "The folder contains invalid characters.";
        if (!Path.IsPathFullyQualified(folder.Trim()) && !folder.Trim().StartsWith('/'))
            return "Use a full path (for example C:\\src\\api.worktrees).";
        return null;
    }
}
