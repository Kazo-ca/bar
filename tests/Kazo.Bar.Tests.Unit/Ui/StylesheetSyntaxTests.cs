namespace Kazo.Bar.Tests.Unit.Ui;

using System.Text.RegularExpressions;

/// <summary>
/// Guards against unbalanced braces in the shared stylesheets: a missing "}" silently
/// pulls every following rule into the previous block (e.g. a max-width media query),
/// so those rules stop applying on desktop without any build or runtime error.
/// </summary>
[TestFixture]
public class StylesheetSyntaxTests
{
    private static readonly string[] Stylesheets =
    {
        "src/Kazo.Bar.UI/wwwroot/css/toolbar.css",
        "src/Kazo.Bar.UI/wwwroot/css/themes.css",
        "src/Kazo.Bar.UI/wwwroot/css/git-manager.css",
        "src/Kazo.Bar.Windows/wwwroot/css/wpf-host.css",
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Kazo.Bar.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (Kazo.Bar.slnx) not found.");
    }

    [TestCaseSource(nameof(Stylesheets))]
    public void Braces_are_balanced(string relativePath)
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), relativePath));
        css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        css = Regex.Replace(css, "\"(?:[^\"\\\\]|\\\\.)*\"|'(?:[^'\\\\]|\\\\.)*'", "\"\"");

        int depth = 0, line = 1;
        foreach (var c in css)
        {
            if (c == '\n') line++;
            else if (c == '{') depth++;
            else if (c == '}' && --depth < 0) Assert.Fail($"{relativePath}: unexpected '}}' near line {line} (comments stripped).");
        }

        Assert.That(depth, Is.EqualTo(0), $"{relativePath}: {depth} unclosed '{{'.");
    }
}
