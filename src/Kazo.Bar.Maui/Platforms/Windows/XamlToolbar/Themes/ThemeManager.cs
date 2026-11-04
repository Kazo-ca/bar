using WinBrush = Microsoft.UI.Xaml.Media.Brush;
using WinSolid = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinLinear = Microsoft.UI.Xaml.Media.LinearGradientBrush;
using Microsoft.UI.Xaml.Media;

namespace Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.Themes;

/// <summary>Available toolbar visual themes matching the design spec.</summary>
public enum ToolbarTheme { Light, Dark, Green }

/// <summary>
/// Complete color palette for a toolbar theme.
/// All colors are defined as WinUI3 Brushes ready for binding.
/// </summary>
public class ThemeColors
{
    // Shell (outer capsule)
    public WinBrush ShellBackground { get; init; } = null!;
    public WinBrush ShellBorder { get; init; } = null!;

    // Project pill
    public WinBrush ProjectPillBackground { get; init; } = null!;
    public WinBrush ProjectPillText { get; init; } = null!;

    // Git pill
    public WinBrush GitPillBackground { get; init; } = null!;
    public WinBrush GitPillText { get; init; } = null!;

    // Action buttons
    public WinBrush ActionButtonBackground { get; init; } = null!;
    public WinBrush ActionButtonForeground { get; init; } = null!;
    public WinBrush ActionButtonHover { get; init; } = null!;

    // System pill (timer + settings)
    public WinBrush SystemPillBackground { get; init; } = null!;
    public WinBrush SystemPillText { get; init; } = null!;
    public WinBrush SystemPillSecondary { get; init; } = null!;

    // Dropdown / flyout
    public WinBrush DropdownBackground { get; init; } = null!;
    public WinBrush DropdownBorder { get; init; } = null!;
    public WinBrush DropdownItemHover { get; init; } = null!;
    public WinBrush DropdownText { get; init; } = null!;
}

/// <summary>
/// Manages the 3 toolbar themes: Light, Dark, Green.
/// Colors are derived from the design reference (doc/Toolbar-design-exemple.png).
/// </summary>
public static class ThemeManager
{
    public static ThemeColors GetTheme(ToolbarTheme theme) => theme switch
    {
        ToolbarTheme.Light => CreateLightTheme(),
        ToolbarTheme.Dark => CreateDarkTheme(),
        ToolbarTheme.Green => CreateGreenTheme(),
        _ => CreateLightTheme(),
    };

    // ── Light Theme ──────────────────────────────────────────────────
    private static ThemeColors CreateLightTheme() => new()
    {
        ShellBackground = MakeLinear(C(220, 225, 235, 190), C(235, 238, 245, 200)),
        ShellBorder = Solid(C(255, 255, 255, 115)),

        ProjectPillBackground = MakeLinear(C(56, 132, 244), C(30, 100, 220)),
        ProjectPillText = Solid(C(255, 255, 255)),

        GitPillBackground = MakeLinear(C(50, 60, 80, 220), C(35, 45, 65, 235)),
        GitPillText = Solid(C(255, 255, 255, 230)),

        ActionButtonBackground = Solid(C(255, 255, 255, 60)),
        ActionButtonForeground = Solid(C(80, 90, 105)),
        ActionButtonHover = Solid(C(255, 255, 255, 120)),

        SystemPillBackground = MakeLinear(C(60, 65, 75, 220), C(45, 50, 60, 240)),
        SystemPillText = Solid(C(255, 255, 255, 220)),
        SystemPillSecondary = Solid(C(255, 255, 255, 140)),

        DropdownBackground = Solid(C(45, 50, 60, 245)),
        DropdownBorder = Solid(C(255, 255, 255, 40)),
        DropdownItemHover = Solid(C(255, 255, 255, 20)),
        DropdownText = Solid(C(255, 255, 255, 230)),
    };

    // ── Dark Theme ───────────────────────────────────────────────────
    private static ThemeColors CreateDarkTheme() => new()
    {
        ShellBackground = MakeLinear(C(30, 34, 42, 240), C(24, 28, 36, 250)),
        ShellBorder = Solid(C(255, 255, 255, 30)),

        ProjectPillBackground = MakeLinear(C(35, 70, 130), C(25, 55, 105)),
        ProjectPillText = Solid(C(255, 255, 255)),

        GitPillBackground = MakeLinear(C(35, 40, 55, 240), C(25, 30, 42, 250)),
        GitPillText = Solid(C(255, 255, 255, 200)),

        ActionButtonBackground = Solid(C(255, 255, 255, 15)),
        ActionButtonForeground = Solid(C(255, 255, 255, 140)),
        ActionButtonHover = Solid(C(255, 255, 255, 35)),

        SystemPillBackground = MakeLinear(C(40, 44, 52, 240), C(30, 34, 42, 250)),
        SystemPillText = Solid(C(255, 255, 255, 200)),
        SystemPillSecondary = Solid(C(255, 255, 255, 120)),

        DropdownBackground = Solid(C(35, 40, 50, 250)),
        DropdownBorder = Solid(C(255, 255, 255, 25)),
        DropdownItemHover = Solid(C(255, 255, 255, 15)),
        DropdownText = Solid(C(255, 255, 255, 210)),
    };

    // ── Green Theme ──────────────────────────────────────────────────
    private static ThemeColors CreateGreenTheme() => new()
    {
        ShellBackground = MakeLinear(C(75, 130, 70, 200), C(55, 105, 50, 220)),
        ShellBorder = Solid(C(255, 255, 255, 60)),

        ProjectPillBackground = MakeLinear(C(90, 175, 80), C(65, 145, 55)),
        ProjectPillText = Solid(C(255, 255, 255)),

        GitPillBackground = MakeLinear(C(50, 95, 45, 230), C(38, 75, 35, 245)),
        GitPillText = Solid(C(255, 255, 255, 220)),

        ActionButtonBackground = Solid(C(255, 255, 255, 30)),
        ActionButtonForeground = Solid(C(255, 255, 255, 180)),
        ActionButtonHover = Solid(C(255, 255, 255, 60)),

        SystemPillBackground = MakeLinear(C(45, 80, 40, 230), C(35, 65, 30, 245)),
        SystemPillText = Solid(C(255, 255, 255, 210)),
        SystemPillSecondary = Solid(C(255, 255, 255, 130)),

        DropdownBackground = Solid(C(38, 65, 35, 250)),
        DropdownBorder = Solid(C(255, 255, 255, 30)),
        DropdownItemHover = Solid(C(255, 255, 255, 18)),
        DropdownText = Solid(C(255, 255, 255, 220)),
    };

    // ── Helpers ──────────────────────────────────────────────────────
    private static global::Windows.UI.Color C(byte r, byte g, byte b, byte a = 255)
        => global::Windows.UI.Color.FromArgb(a, r, g, b);

    private static WinSolid Solid(global::Windows.UI.Color color) => new(color);

    private static WinLinear MakeLinear(global::Windows.UI.Color start, global::Windows.UI.Color end)
        => new()
        {
            StartPoint = new global::Windows.Foundation.Point(0, 0),
            EndPoint = new global::Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new Microsoft.UI.Xaml.Media.GradientStop { Color = start, Offset = 0 },
                new Microsoft.UI.Xaml.Media.GradientStop { Color = end, Offset = 1 },
            }
        };
}
