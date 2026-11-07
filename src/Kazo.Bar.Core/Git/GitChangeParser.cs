namespace Kazo.Bar.Core.Git;

using Kazo.Bar.Core.Models;

/// <summary>
/// Builds a <see cref="GitChangeSet"/> from <c>git status --porcelain=v1 -z</c> and the
/// <c>git diff --numstat -z</c> outputs of the index and of the working tree (US5.7).
/// </summary>
/// <remarks>
/// Status entries are <c>XY path\0</c>; a rename or copy adds the old path as the next entry
/// (<c>XY new\0old\0</c>). X is the index state, Y the working-tree state. Numstat entries are
/// <c>added\tdeleted\tpath\0</c>, or <c>added\tdeleted\t\0old\0new\0</c> for a rename; a binary
/// file has <c>-</c> counts. Paths are relative to the repository root.
/// </remarks>
public static class GitChangeParser
{
    private static readonly HashSet<string> UnmergedStates = ["DD", "AU", "UD", "UA", "DU", "AA", "UU"];

    /// <summary>Line counts of one file in a numstat output; null counts for a binary file.</summary>
    public sealed record LineStat(int? Added, int? Deleted)
    {
        public bool IsBinary => Added == null;
    }

    /// <summary>Reads <c>git diff --numstat -z</c>: line counts by (new) path.</summary>
    public static IReadOnlyDictionary<string, LineStat> ParseNumstat(string? numstat)
    {
        var result = new Dictionary<string, LineStat>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(numstat)) return result;

        var parts = numstat.Split('\0');
        for (var i = 0; i < parts.Length; i++)
        {
            var fields = parts[i].Split('\t', 3);
            if (fields.Length < 3) continue;
            var stat = new LineStat(ParseCount(fields[0]), ParseCount(fields[1]));
            var path = fields[2];
            if (path.Length == 0)
            {
                // Rename: the old then the new path follow as separate entries.
                if (i + 2 >= parts.Length) break;
                path = parts[i + 2];
                i += 2;
            }
            result[path] = stat;
        }
        return result;
    }

    /// <summary>
    /// Reads <c>git status --porcelain=v1 -z</c> and attaches the line counts of the index
    /// (<paramref name="stagedStats"/>) and of the working tree (<paramref name="unstagedStats"/>).
    /// Untracked files get their counts from <paramref name="untrackedStats"/> (every line is new).
    /// </summary>
    public static GitChangeSet Parse(
        string? porcelain,
        IReadOnlyDictionary<string, LineStat>? stagedStats = null,
        IReadOnlyDictionary<string, LineStat>? unstagedStats = null,
        Func<string, LineStat?>? untrackedStats = null)
    {
        if (string.IsNullOrEmpty(porcelain)) return GitChangeSet.Empty;

        var staged = new List<GitFileChange>();
        var unstaged = new List<GitFileChange>();
        var entries = porcelain.Split('\0');
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length < 4 || entry[2] != ' ') continue;
            var x = entry[0];
            var y = entry[1];
            var path = entry[3..];
            string? oldPath = null;
            if (x is 'R' or 'C' || y is 'R' or 'C')
            {
                oldPath = i + 1 < entries.Length ? entries[i + 1] : null;
                i++;
            }

            if (x == '!' ) continue;
            if (x == '?')
            {
                unstaged.Add(WithStat(new GitFileChange { Path = path, Kind = GitChangeKind.Untracked }, untrackedStats?.Invoke(path)));
                continue;
            }
            if (UnmergedStates.Contains($"{x}{y}"))
            {
                unstaged.Add(WithStat(new GitFileChange { Path = path, Kind = GitChangeKind.Conflicted }, Lookup(unstagedStats, path)));
                continue;
            }
            if (KindOf(x) is { } indexKind)
            {
                staged.Add(WithStat(new GitFileChange
                {
                    Path = path,
                    OldPath = x is 'R' or 'C' ? oldPath : null,
                    Kind = indexKind,
                    IsStaged = true,
                }, Lookup(stagedStats, path)));
            }
            if (KindOf(y) is { } workKind)
            {
                unstaged.Add(WithStat(new GitFileChange
                {
                    Path = path,
                    OldPath = y is 'R' or 'C' ? oldPath : null,
                    Kind = workKind,
                }, Lookup(unstagedStats, path)));
            }
        }

        return new GitChangeSet
        {
            Staged = staged.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToList(),
            Unstaged = unstaged.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    private static GitChangeKind? KindOf(char state) => state switch
    {
        'M' => GitChangeKind.Modified,
        'A' => GitChangeKind.Added,
        'D' => GitChangeKind.Deleted,
        'R' => GitChangeKind.Renamed,
        'C' => GitChangeKind.Copied,
        'T' => GitChangeKind.TypeChanged,
        _ => null,
    };

    private static LineStat? Lookup(IReadOnlyDictionary<string, LineStat>? stats, string path) =>
        stats != null && stats.TryGetValue(path, out var stat) ? stat : null;

    private static GitFileChange WithStat(GitFileChange change, LineStat? stat) => stat == null
        ? change
        : change with { LinesAdded = stat.Added ?? 0, LinesDeleted = stat.Deleted ?? 0, IsBinary = stat.IsBinary };

    private static int? ParseCount(string text) => int.TryParse(text, out var value) ? value : null;
}
