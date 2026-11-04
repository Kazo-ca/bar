namespace DevToolbar.UI.Services;

using DevToolbar.Core.Models;

/// <summary>
/// Generates CSS variables from the active project's theme configuration.
/// </summary>
public class ThemeService
{
    private ThemeConfig _currentTheme = new();

    public void SetTheme(ThemeConfig theme)
    {
        _currentTheme = theme;
        OnThemeChanged?.Invoke();
    }

    /// <summary>Active theme (US3.3), always a known name; set as <c>data-theme</c> on the toolbar.</summary>
    public string ThemeName => ThemeNames.Normalize(_currentTheme.Name);

    public string GetCssVariables() =>
        $"--accent-color: {_currentTheme.AccentColor}; --font-family: {_currentTheme.FontFamily};";

    /// <summary>
    /// Returns the project's custom CSS, with '&lt;' removed so config content
    /// cannot close the surrounding &lt;style&gt; element and inject markup.
    /// </summary>
    public string GetCustomCss()
    {
        var css = _currentTheme.CustomCss;
        if (string.IsNullOrWhiteSpace(css)) return string.Empty;
        // CSS never needs '<'; dropping it prevents "</style>" break-outs.
        return css.Replace("<", string.Empty);
    }

    public event Action? OnThemeChanged;
}
