using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.ViewModels;
using WinSolid = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.Controls;

/// <summary>
/// System pill (Zone 4). Contains: Timer display + Settings gear.
/// Dark rounded pill on the right side.
/// </summary>
public class SystemPill : UserControl
{
    private readonly TextBlock _timerText;
    private readonly FontIcon _timerIcon;
    private readonly FontIcon _settingsIcon;
    private readonly Microsoft.UI.Xaml.Controls.Border _pill;
    private ToolbarViewModel? _vm;

    public SystemPill()
    {
        _timerIcon = new FontIcon
        {
            Glyph = "\uE823",
            FontSize = 16,
        };

        _timerText = new TextBlock
        {
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        };

        _settingsIcon = new FontIcon
        {
            Glyph = "\uE713",
            FontSize = 16,
        };

        var settingsBtn = new Microsoft.UI.Xaml.Controls.Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(16),
            Child = _settingsIcon,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
        };
        _settingsIcon.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center;
        _settingsIcon.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
        ToolTipService.SetToolTip(settingsBtn, "Settings");

        settingsBtn.PointerEntered += (s, _) =>
        {
            if (s is Microsoft.UI.Xaml.Controls.Border b)
                b.Background = new WinSolid(global::Windows.UI.Color.FromArgb(30, 255, 255, 255));
        };
        settingsBtn.PointerExited += (s, _) =>
        {
            if (s is Microsoft.UI.Xaml.Controls.Border b)
                b.Background = null;
        };
        settingsBtn.PointerPressed += (_, _) => _vm?.OpenSettingsCommand.Execute(null);

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
        };
        contentPanel.Children.Add(_timerIcon);
        contentPanel.Children.Add(_timerText);
        contentPanel.Children.Add(settingsBtn);

        _pill = new Microsoft.UI.Xaml.Controls.Border
        {
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(28),
            Height = 44,
            Padding = new Microsoft.UI.Xaml.Thickness(16, 0, 12, 0),
            Child = contentPanel,
        };

        Canvas.SetZIndex(this, 20);
        Content = _pill;
    }

    public void ApplyViewModel(ToolbarViewModel vm)
    {
        _vm = vm;

        _timerText.SetBinding(TextBlock.TextProperty,
            new Microsoft.UI.Xaml.Data.Binding { Source = vm, Path = new PropertyPath("TimerText"), Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay });

        ApplyTheme(vm.Theme);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Theme))
                ApplyTheme(vm.Theme);
        };
    }

    private void ApplyTheme(Themes.ThemeColors theme)
    {
        _pill.Background = theme.SystemPillBackground;
        _timerIcon.Foreground = theme.SystemPillSecondary;
        _timerText.Foreground = theme.SystemPillText;
        _settingsIcon.Foreground = theme.SystemPillSecondary;
    }
}
