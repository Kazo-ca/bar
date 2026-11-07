namespace Kazo.Bar.Core.Git;

using System.Text.RegularExpressions;
using Kazo.Bar.Core.Models;

/// <summary>
/// Reads the unified diff printed by <c>git diff</c> for one file into hunks and numbered
/// lines (US5.7). File headers (<c>diff --git</c>, <c>index</c>, <c>---</c>, <c>+++</c>) are
/// skipped; "Binary files ... differ" marks the file as binary.
/// </summary>
public static partial class GitDiffParser
{
    /// <summary>Most diff lines kept; a larger diff is cut and flagged as truncated.</summary>
    public const int MaxLines = 5000;

    [GeneratedRegex(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@")]
    private static partial Regex HunkHeader();

    /// <summary>Header of a combined diff (a file with merge conflicts): <c>@@@ -a -b +c @@@</c>.</summary>
    [GeneratedRegex(@"^@@@ -(\d+)(?:,\d+)? -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@@")]
    private static partial Regex CombinedHunkHeader();

    public static GitFileDiff Parse(string path, string? diff)
    {
        if (string.IsNullOrEmpty(diff)) return new GitFileDiff { Path = path };

        var hunks = new List<GitDiffHunk>();
        List<GitDiffLine>? lines = null;
        string? header = null;
        int oldNumber = 0, newNumber = 0, total = 0;
        var binary = false;
        var truncated = false;
        var prefixLength = 1;

        foreach (var raw in diff.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var match = HunkHeader().Match(line);
            var combined = !match.Success && (match = CombinedHunkHeader().Match(line)).Success;
            if (match.Success)
            {
                prefixLength = combined ? 2 : 1;
                if (lines != null) hunks.Add(new GitDiffHunk(header!, lines));
                header = line;
                lines = new List<GitDiffLine>();
                oldNumber = int.Parse(match.Groups[1].Value);
                newNumber = int.Parse(match.Groups[2].Value);
                continue;
            }
            if (lines == null)
            {
                if (line.StartsWith("Binary files ", StringComparison.Ordinal) || line.StartsWith("GIT binary patch", StringComparison.Ordinal))
                    binary = true;
                continue;
            }
            if (total >= MaxLines)
            {
                truncated = true;
                break;
            }
            if (line.Length == 0 && raw.Length == 0)
                continue; // The empty string after the last '\n'.
            total++;
            var prefix = line.Length >= prefixLength ? line[..prefixLength] : line.PadRight(prefixLength);
            var text = line.Length > prefixLength ? line[prefixLength..] : string.Empty;
            var marker = prefix.Contains('+') ? '+' : prefix.Contains('-') ? '-' : prefix[0] == '\\' ? '\\' : ' ';
            switch (marker)
            {
                case '+':
                    lines.Add(new GitDiffLine(GitDiffLineKind.Added, null, newNumber++, text));
                    break;
                case '-':
                    lines.Add(new GitDiffLine(GitDiffLineKind.Deleted, oldNumber++, null, text));
                    break;
                case '\\':
                    lines.Add(new GitDiffLine(GitDiffLineKind.Note, null, null, line.TrimStart('\\', ' ')));
                    break;
                default:
                    lines.Add(new GitDiffLine(GitDiffLineKind.Context, oldNumber++, newNumber++, text));
                    break;
            }
        }
        if (lines != null) hunks.Add(new GitDiffHunk(header!, lines));
        return new GitFileDiff { Path = path, IsBinary = binary, Hunks = hunks, IsTruncated = truncated };
    }

    /// <summary>Diff of a new, untracked file: every line is added.</summary>
    public static GitFileDiff ForNewFile(string path, string content)
    {
        var all = content.Split('\n');
        var count = content.EndsWith('\n') ? all.Length - 1 : all.Length;
        var kept = Math.Min(count, MaxLines);
        var lines = new List<GitDiffLine>(kept);
        for (var i = 0; i < kept; i++)
            lines.Add(new GitDiffLine(GitDiffLineKind.Added, null, i + 1, all[i].TrimEnd('\r')));
        var hunks = count == 0 ? Array.Empty<GitDiffHunk>() : new[] { new GitDiffHunk($"@@ -0,0 +1,{count} @@", lines) };
        return new GitFileDiff { Path = path, Hunks = hunks, IsTruncated = count > kept };
    }
}
