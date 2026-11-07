namespace Kazo.Bar.Tests.Unit.Configuration;

using Kazo.Bar.Core.Configuration;
using Kazo.Bar.Core.Models;

[TestFixture]
public class ProjectSettingsDraftTests
{
    private static ProjectConfig SampleProject() => new()
    {
        Id = "api",
        Name = "API",
        Path = @"C:\src\api",
        ProjectType = "WebApi",
        DefaultBranch = "develop",
        RepositoryUrl = "https://github.com/example/api",
        Theme = new ThemeConfig { Name = ThemeNames.Dark, AccentColor = "#112233", FontFamily = "Consolas" },
        EnabledPlugins = ["git-tools"],
        Actions = [new ActionConfig { Label = "Code", ProcessPath = "code.exe" }],
        GitRepositories =
        [
            new GitRepository { Name = "api", Path = @"C:\src\api\" },
            new GitRepository { Name = "lib", Path = @"C:\src\lib" },
        ],
    };

    [Test]
    public void From_CopiesTheEditableValues()
    {
        var draft = ProjectSettingsDraft.From(SampleProject());

        Assert.That(draft.Name, Is.EqualTo("API"));
        Assert.That(draft.Path, Is.EqualTo(@"C:\src\api"));
        Assert.That(draft.ThemeName, Is.EqualTo(ThemeNames.Dark));
        Assert.That(draft.AccentColor, Is.EqualTo("#112233"));
        Assert.That(draft.FontFamily, Is.EqualTo("Consolas"));
        Assert.That(draft.CustomCss, Is.Empty);
        Assert.That(draft.Validate(), Is.Empty);
    }

    [Test]
    public void From_ReplacesAnUnreadableColor_WithTheDefault()
    {
        var project = SampleProject();
        project.Theme.AccentColor = "blue";

        Assert.That(ProjectSettingsDraft.From(project).AccentColor, Is.EqualTo(ProjectSettingsDraft.DefaultAccentColor));
    }

    [Test]
    public void ApplyTo_BuildsANewProject_AndLeavesTheOriginalUnchanged()
    {
        var project = SampleProject();
        var draft = ProjectSettingsDraft.From(project) with
        {
            Name = "  Public API ",
            ThemeName = "GREEN",
            AccentColor = "#a1b2c3",
            CustomCss = ".toolbar-shell { gap: 2px; }",
        };

        var saved = draft.ApplyTo(project);

        Assert.That(saved, Is.Not.SameAs(project));
        Assert.That(saved.Id, Is.EqualTo("api"));
        Assert.That(saved.Name, Is.EqualTo("Public API"));
        Assert.That(saved.Theme.Name, Is.EqualTo(ThemeNames.Green));
        Assert.That(saved.Theme.AccentColor, Is.EqualTo("#A1B2C3"));
        Assert.That(saved.Theme.CustomCss, Is.EqualTo(".toolbar-shell { gap: 2px; }"));
        Assert.That(saved.EnabledPlugins, Is.EqualTo(project.EnabledPlugins).And.Not.SameAs(project.EnabledPlugins));
        Assert.That(saved.Actions, Has.Count.EqualTo(1));
        Assert.That(project.Name, Is.EqualTo("API"));
        Assert.That(project.Theme.Name, Is.EqualTo(ThemeNames.Dark));
    }

    [Test]
    public void ApplyTo_MovesTheRepositoryThatFollowedTheOldFolder()
    {
        var project = SampleProject();
        var saved = (ProjectSettingsDraft.From(project) with { Path = @" D:\work\api " }).ApplyTo(project);

        Assert.That(saved.Path, Is.EqualTo(@"D:\work\api"));
        Assert.That(saved.GitRepositories.Select(r => r.Path), Is.EqualTo(new[] { @"D:\work\api", @"C:\src\lib" }));
    }

    [Test]
    public void ApplyTo_KeepsRepositories_WhenTheFolderIsUnchanged()
    {
        var project = SampleProject();
        var saved = ProjectSettingsDraft.From(project).ApplyTo(project);

        Assert.That(saved.GitRepositories.Select(r => r.Path), Is.EqualTo(new[] { @"C:\src\api\", @"C:\src\lib" }));
    }

    [Test]
    public void ApplyTo_ClearsBlankCustomCss_AndKeepsTheFont_WhenItIsBlank()
    {
        var project = SampleProject();
        project.Theme.CustomCss = "x";
        var saved = (ProjectSettingsDraft.From(project) with { CustomCss = "  ", FontFamily = " " }).ApplyTo(project);

        Assert.That(saved.Theme.CustomCss, Is.Null);
        Assert.That(saved.Theme.FontFamily, Is.EqualTo("Consolas"));
    }

    [TestCase(nameof(ProjectSettingsDraft.Name), "")]
    [TestCase(nameof(ProjectSettingsDraft.Path), "  ")]
    [TestCase(nameof(ProjectSettingsDraft.Path), "C:\\a\"b")]
    [TestCase(nameof(ProjectSettingsDraft.DefaultBranch), "")]
    [TestCase(nameof(ProjectSettingsDraft.DefaultBranch), "feature branch")]
    [TestCase(nameof(ProjectSettingsDraft.DefaultBranch), "a..b")]
    [TestCase(nameof(ProjectSettingsDraft.DefaultBranch), "-main")]
    [TestCase(nameof(ProjectSettingsDraft.DefaultBranch), "main.lock")]
    [TestCase(nameof(ProjectSettingsDraft.RepositoryUrl), "ftp://example.com/repo")]
    [TestCase(nameof(ProjectSettingsDraft.RepositoryUrl), "not a url")]
    [TestCase(nameof(ProjectSettingsDraft.AccentColor), "#12345")]
    [TestCase(nameof(ProjectSettingsDraft.AccentColor), "red")]
    [TestCase(nameof(ProjectSettingsDraft.CustomCss), "</style><script>alert(1)</script>")]
    public void Validate_ReportsTheInvalidField(string field, string value)
    {
        var draft = ProjectSettingsDraft.From(SampleProject());
        draft = field switch
        {
            nameof(ProjectSettingsDraft.Name) => draft with { Name = value },
            nameof(ProjectSettingsDraft.Path) => draft with { Path = value },
            nameof(ProjectSettingsDraft.DefaultBranch) => draft with { DefaultBranch = value },
            nameof(ProjectSettingsDraft.RepositoryUrl) => draft with { RepositoryUrl = value },
            nameof(ProjectSettingsDraft.AccentColor) => draft with { AccentColor = value },
            _ => draft with { CustomCss = value },
        };

        var errors = draft.Validate();

        Assert.That(errors.Keys, Is.EqualTo(new[] { field }));
        Assert.Throws<ArgumentException>(() => draft.ApplyTo(SampleProject()));
    }

    [TestCase("main")]
    [TestCase("release/1.0")]
    [TestCase("feature/US-12_new")]
    public void Validate_AcceptsUsualBranchNames(string branch)
    {
        var draft = ProjectSettingsDraft.From(SampleProject()) with { DefaultBranch = branch };

        Assert.That(draft.Validate(), Is.Empty);
    }

    [Test]
    public void Validate_RejectsTooLongNameAndType_AndAcceptsAnEmptyUrl()
    {
        var draft = ProjectSettingsDraft.From(SampleProject()) with
        {
            Name = new string('n', ProjectFactory.MaxNameLength + 1),
            ProjectType = new string('t', ProjectSettingsDraft.MaxTypeLength + 1),
            RepositoryUrl = "",
        };

        Assert.That(draft.Validate().Keys, Is.EquivalentTo(new[] { nameof(ProjectSettingsDraft.Name), nameof(ProjectSettingsDraft.ProjectType) }));
    }
}
