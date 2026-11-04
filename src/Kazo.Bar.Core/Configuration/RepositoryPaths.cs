namespace Kazo.Bar.Core.Configuration;

/// <summary>Path helpers shared by project creation and the Git plugin.</summary>
public static class RepositoryPaths
{
    /// <summary>Characters refused in a local folder (they would break quoting or the JSON file).</summary>
    public static readonly char[] ForbiddenChars = ['"', '\r', '\n', '\0'];

    /// <summary>
    /// Case-insensitive path comparison that ignores trailing separators and treats
    /// <c>/</c> and <c>\</c> alike (<c>git</c> prints <c>C:/src/api</c> on Windows).
    /// </summary>
    public static bool SamePath(string a, string b) =>
        string.Equals(TrimSeparators(a).Replace('\\', '/'), TrimSeparators(b).Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>Last folder name of a path (<c>C:\src\api\</c> gives <c>api</c>).</summary>
    public static string NameFromPath(string path)
    {
        var trimmed = TrimSeparators(path);
        var lastSeparator = trimmed.LastIndexOfAny(['/', '\\']);
        return lastSeparator >= 0 ? trimmed[(lastSeparator + 1)..] : trimmed;
    }

    private static string TrimSeparators(string path) => path.Trim().TrimEnd('/', '\\');
}
