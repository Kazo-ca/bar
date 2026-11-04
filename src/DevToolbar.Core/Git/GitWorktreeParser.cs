namespace DevToolbar.Core.Git;

using DevToolbar.Core.Models;

/// <summary>
/// Reads the output of <c>git worktree list --porcelain</c> (US5.6). Each working tree is a
/// block of lines separated by an empty line: <c>worktree &lt;path&gt;</c>, then <c>HEAD &lt;sha&gt;</c>,
/// then <c>branch refs/heads/&lt;name&gt;</c> or <c>detached</c> (or <c>bare</c>). Bare entries
/// have no files and are left out. The first entry is the main working tree.
/// </summary>
public static class GitWorktreeParser
{
    private const int ShortShaLength = 7;
    private const string BranchPrefix = "refs/heads/";

    public static IReadOnlyList<GitWorktree> Parse(string? porcelain)
    {
        if (string.IsNullOrWhiteSpace(porcelain))
            return Array.Empty<GitWorktree>();

        var result = new List<GitWorktree>();
        var first = true;
        foreach (var block in SplitBlocks(porcelain))
        {
            string? path = null, head = null, branch = null;
            var bare = false;
            foreach (var line in block)
            {
                if (line.StartsWith("worktree ", StringComparison.Ordinal)) path = line["worktree ".Length..].Trim();
                else if (line.StartsWith("HEAD ", StringComparison.Ordinal)) head = line["HEAD ".Length..].Trim();
                else if (line.StartsWith("branch ", StringComparison.Ordinal)) branch = ShortBranch(line["branch ".Length..].Trim());
                else if (line == "bare") bare = true;
            }
            if (path == null) continue;
            var isMain = first;
            first = false;
            if (bare) continue;
            result.Add(new GitWorktree
            {
                Path = path,
                Branch = branch,
                Head = head is { Length: > ShortShaLength } ? head[..ShortShaLength] : head,
                IsMain = isMain,
            });
        }
        return result;
    }

    private static string ShortBranch(string reference) =>
        reference.StartsWith(BranchPrefix, StringComparison.Ordinal) ? reference[BranchPrefix.Length..] : reference;

    private static IEnumerable<List<string>> SplitBlocks(string text)
    {
        var current = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (current.Count > 0) yield return current;
                current = new List<string>();
                continue;
            }
            current.Add(line);
        }
        if (current.Count > 0) yield return current;
    }
}
