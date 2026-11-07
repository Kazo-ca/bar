namespace Kazo.Bar.Core.Git;

/// <summary>
/// Checks a new branch name against Git's reference rules (<c>git check-ref-format --branch</c>)
/// before any command runs, so the user gets a clear message (US5.7).
/// </summary>
public static class GitBranchNames
{
    private const int MaxLength = 200;
    private static readonly char[] Forbidden = [' ', '~', '^', ':', '?', '*', '[', '\\', '\t', '\r', '\n', '\0'];

    /// <summary>Returns a user-facing error, or null when the name is valid.</summary>
    public static string? Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "The branch name is required.";
        if (name.Length > MaxLength) return $"The branch name must be {MaxLength} characters or fewer.";
        if (name.IndexOfAny(Forbidden) >= 0 || name.Any(char.IsControl))
            return "The branch name cannot contain spaces or any of ~ ^ : ? * [ \\.";
        if (name.StartsWith('-')) return "The branch name cannot start with '-'.";
        if (name.StartsWith('/') || name.EndsWith('/') || name.Contains("//"))
            return "The branch name cannot start or end with '/' or contain '//'.";
        if (name.EndsWith('.') || name.Contains("..") || name.Contains("@{") || name == "@")
            return "The branch name cannot end with '.', contain '..' or '@{', or be '@'.";
        if (name.Split('/').Any(part => part.StartsWith('.') || part.EndsWith(".lock", StringComparison.Ordinal)))
            return "No part of the branch name can start with '.' or end with '.lock'.";
        if (name == "HEAD") return "'HEAD' is not a valid branch name.";
        return null;
    }
}
