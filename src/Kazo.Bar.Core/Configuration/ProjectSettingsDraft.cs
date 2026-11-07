namespace Kazo.Bar.Core.Configuration;

using System.Text.RegularExpressions;
using Kazo.Bar.Core.Models;

/// <summary>
/// Editable copy of a project's own settings (display name, folder, type, branch, repository
/// URL, theme). The project settings page edits a draft, then <see cref="ApplyTo"/> builds the
/// project to save: nothing changes until the user saves, and Cancel just drops the draft.
/// Pure logic, no I/O.
/// </summary>
public sealed partial record ProjectSettingsDraft
{
    public const int MaxTypeLength = 40;
    public const int MaxBranchLength = 200;
    public const int MaxCustomCssLength = 20_000;
    public const string DefaultAccentColor = "#0078D7";

    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string ProjectType { get; init; } = string.Empty;
    public string DefaultBranch { get; init; } = "main";
    public string RepositoryUrl { get; init; } = string.Empty;
    public string ThemeName { get; init; } = ThemeNames.Light;
    public string AccentColor { get; init; } = DefaultAccentColor;
    public string FontFamily { get; init; } = string.Empty;
    public string CustomCss { get; init; } = string.Empty;

    /// <summary>Draft holding the current values of <paramref name="project"/>.</summary>
    public static ProjectSettingsDraft From(ProjectConfig project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var theme = project.Theme ?? new ThemeConfig();
        return new ProjectSettingsDraft
        {
            Name = project.Name,
            Path = project.Path,
            ProjectType = project.ProjectType,
            DefaultBranch = project.DefaultBranch,
            RepositoryUrl = project.RepositoryUrl,
            ThemeName = ThemeNames.Normalize(theme.Name),
            AccentColor = IsHexColor(theme.AccentColor) ? theme.AccentColor : DefaultAccentColor,
            FontFamily = theme.FontFamily,
            CustomCss = theme.CustomCss ?? string.Empty,
        };
    }

    /// <summary>Returns the user-facing errors of the draft, keyed by field; empty when it can be saved.</summary>
    public IReadOnlyDictionary<string, string> Validate()
    {
        var errors = new Dictionary<string, string>();
        var name = Name.Trim();
        if (name.Length == 0)
            errors[nameof(Name)] = "The display name is required.";
        else if (name.Length > ProjectFactory.MaxNameLength)
            errors[nameof(Name)] = $"The display name must be {ProjectFactory.MaxNameLength} characters or fewer.";

        var path = Path.Trim();
        if (path.Length == 0)
            errors[nameof(Path)] = "The local folder is required.";
        else if (path.IndexOfAny(RepositoryPaths.ForbiddenChars) >= 0)
            errors[nameof(Path)] = "The local folder contains invalid characters.";

        if (ProjectType.Trim().Length > MaxTypeLength)
            errors[nameof(ProjectType)] = $"The type must be {MaxTypeLength} characters or fewer.";

        var branch = DefaultBranch.Trim();
        if (branch.Length == 0)
            errors[nameof(DefaultBranch)] = "The default branch is required.";
        else if (branch.Length > MaxBranchLength || !BranchPattern().IsMatch(branch))
            errors[nameof(DefaultBranch)] = "The default branch is not a valid branch name.";

        var url = RepositoryUrl.Trim();
        if (url.Length > 0 && !IsRepositoryUrl(url))
            errors[nameof(RepositoryUrl)] = "The repository URL must start with https:// or http://.";

        if (!IsHexColor(AccentColor))
            errors[nameof(AccentColor)] = "The color must look like #1E90FF.";

        if (CustomCss.Length > MaxCustomCssLength)
            errors[nameof(CustomCss)] = $"The custom CSS must be {MaxCustomCssLength} characters or fewer.";
        else if (CustomCss.Contains("</style", StringComparison.OrdinalIgnoreCase))
            errors[nameof(CustomCss)] = "The custom CSS cannot close the style element.";

        return errors;
    }

    /// <summary>
    /// Builds a new project from <paramref name="project"/> with the draft's values; the original
    /// is not changed. When the folder changes, the repository that pointed at the old folder
    /// follows it, so the Git plugin keeps working.
    /// </summary>
    /// <exception cref="ArgumentException">The draft is invalid (see <see cref="Validate"/>).</exception>
    public ProjectConfig ApplyTo(ProjectConfig project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var errors = Validate();
        if (errors.Count > 0)
            throw new ArgumentException(errors.Values.First(), nameof(project));

        var path = Path.Trim();
        var folderMoved = !RepositoryPaths.SamePath(path, project.Path);
        var oldTheme = project.Theme ?? new ThemeConfig();
        return new ProjectConfig
        {
            Id = project.Id,
            Name = Name.Trim(),
            Path = path,
            ProjectType = ProjectType.Trim(),
            DefaultBranch = DefaultBranch.Trim(),
            RepositoryUrl = RepositoryUrl.Trim(),
            Theme = new ThemeConfig
            {
                Name = ThemeNames.Normalize(ThemeName),
                AccentColor = AccentColor.ToUpperInvariant(),
                FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? oldTheme.FontFamily : FontFamily.Trim(),
                CustomCss = string.IsNullOrWhiteSpace(CustomCss) ? null : CustomCss,
            },
            EnabledPlugins = [.. project.EnabledPlugins],
            Actions = [.. project.Actions],
            GitRepositories = project.GitRepositories
                .Select(r => folderMoved && RepositoryPaths.SamePath(r.Path, project.Path) ? r with { Path = path } : r)
                .ToList(),
        };
    }

    /// <summary>True for a six-digit hex color such as <c>#1E90FF</c> (what a color input gives).</summary>
    public static bool IsHexColor(string? value) => value is not null && HexColorPattern().IsMatch(value);

    private static bool IsRepositoryUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorPattern();

    // Simplified git ref rules: no whitespace or control characters, none of ~^:?*[\,
    // no "..", "//" or "@{", no leading "-" or "/", no trailing "/" or ".lock".
    [GeneratedRegex(@"^(?!-)(?!/)(?!.*\.\.)(?!.*//)(?!.*/$)(?!.*\.lock$)(?!.*@\{)[^\s~^:?*\[\\\x00-\x1F\x7F]+$")]
    private static partial Regex BranchPattern();
}
