namespace Kazo.Bar.Tests.Unit.Ui;

using System.Text.Json;
using System.Text.RegularExpressions;
using Kazo.Bar.Core.Models;
using Kazo.Bar.UI.Services;

/// <summary>US3.3: themes offered to the user (light, dark, green).</summary>
[TestFixture]
public class ThemeTests
{
    [TestCase("light", "light")]
    [TestCase("dark", "dark")]
    [TestCase("green", "green")]
    [TestCase(" Dark ", "dark")]
    [TestCase("GREEN", "green")]
    [TestCase("purple", "light")]
    [TestCase("", "light")]
    [TestCase(null, "light")]
    public void Normalize_returns_a_known_theme_or_light(string? name, string expected)
    {
        Assert.That(ThemeNames.Normalize(name), Is.EqualTo(expected));
    }

    [Test]
    public void ThemeService_exposes_the_normalized_name_of_the_active_theme()
    {
        var service = new ThemeService();
        Assert.That(service.ThemeName, Is.EqualTo("light"), "default theme");

        service.SetTheme(new ThemeConfig { Name = "Dark" });
        Assert.That(service.ThemeName, Is.EqualTo("dark"));

        service.SetTheme(new ThemeConfig { Name = "unknown" });
        Assert.That(service.ThemeName, Is.EqualTo("light"));
    }

    [Test]
    public void Theme_name_is_read_from_and_written_to_the_project_config()
    {
        var theme = JsonSerializer.Deserialize<ThemeConfig>("""{ "name": "green", "accentColor": "#4CAF50" }""")!;
        Assert.That(theme.Name, Is.EqualTo("green"));

        var json = JsonSerializer.Serialize(new ThemeConfig { Name = ThemeNames.Dark });
        Assert.That(json, Does.Contain("\"name\":\"dark\""));

        var old = JsonSerializer.Deserialize<ThemeConfig>("""{ "accentColor": "#0078D7" }""")!;
        Assert.That(old.Name, Is.EqualTo("light"), "configs written before US3.3 keep the light look");
    }

    [Test]
    public void Every_theme_except_light_has_rules_in_themes_css()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src/Kazo.Bar.UI/wwwroot/css/themes.css"));
        var styled = Regex.Matches(css, "data-theme=\"([a-z]+)\"").Select(m => m.Groups[1].Value).ToHashSet();

        Assert.That(styled, Is.EquivalentTo(ThemeNames.All.Where(n => n != ThemeNames.Light)),
            "light is the base look of toolbar.css; every other theme needs its rules");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Kazo.Bar.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (Kazo.Bar.slnx) not found.");
    }
}
