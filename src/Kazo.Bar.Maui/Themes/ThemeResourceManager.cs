namespace Kazo.Bar.Maui.Themes;

/// <summary>
/// Manages theme switching by swapping ResourceDictionaries in Application.Resources.
/// All controls using {DynamicResource} update automatically when the theme changes.
/// </summary>
public static class ThemeResourceManager
{
    private static ResourceDictionary? _currentTheme;

    /// <summary>
    /// Apply a named theme globally. Supported: "Light", "Dark", "Green".
    /// </summary>
    public static void ApplyTheme(string themeName)
    {
        var app = Application.Current;
        if (app == null) return;

        // Remove the previous theme dictionary
        if (_currentTheme != null)
            app.Resources.MergedDictionaries.Remove(_currentTheme);

        // Create and apply the new theme
        _currentTheme = themeName switch
        {
            "Dark" => new DarkTheme(),
            "Green" => new GreenTheme(),
            _ => new LightTheme(),
        };

        app.Resources.MergedDictionaries.Add(_currentTheme);
    }

    /// <summary>
    /// Initialize the default theme at application startup.
    /// </summary>
    public static void Initialize(string defaultTheme = "Light")
        => ApplyTheme(defaultTheme);
}
