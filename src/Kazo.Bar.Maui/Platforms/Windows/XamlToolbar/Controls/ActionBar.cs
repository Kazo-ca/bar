using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.ViewModels;
using WinSolid = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.Controls;

/// <summary>
/// Action buttons area (Zone 3). Contains: Play, Globe, Folder buttons.
/// Glass-style translucent buttons with subtle borders.
/// </summary>
public class ActionBar : UserControl
{
    private readonly StackPanel _panel;
    private ToolbarViewModel? _vm;

    public ActionBar()
    {
        _panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
            Padding = new Microsoft.UI.Xaml.Thickness(12, 0, 12, 0),
        };

        Canvas.SetZIndex(this, 1);
        Content = _panel;
    }

    public void ApplyViewModel(ToolbarViewModel vm)
    {
        _vm = vm;
        RebuildButtons();

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Theme))
                RebuildButtons();
        };
    }

    private void RebuildButtons()
    {
        if (_vm == null) return;
        _panel.Children.Clear();

        _panel.Children.Add(CreateActionButton("\uE768", "Run / Build", _vm.RunActionCommand));
        _panel.Children.Add(CreateActionButton("\uE774", "Open Browser", _vm.OpenBrowserCommand));
        _panel.Children.Add(CreateActionButton("\uED25", "Open Folder", _vm.OpenFolderCommand));
    }

    private Microsoft.UI.Xaml.Controls.Border CreateActionButton(string glyph, string tooltip, RelayCommand command)
    {
        var theme = _vm!.Theme;

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = 18,
            Foreground = theme.ActionButtonForeground,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
        };

        var btn = new Microsoft.UI.Xaml.Controls.Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(20),
            Background = theme.ActionButtonBackground,
            BorderBrush = new WinSolid(global::Windows.UI.Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Microsoft.UI.Xaml.Thickness(1),
            Child = icon,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
        };

        ToolTipService.SetToolTip(btn, tooltip);

        btn.PointerEntered += (s, _) =>
        {
            if (s is Microsoft.UI.Xaml.Controls.Border b) b.Background = theme.ActionButtonHover;
        };
        btn.PointerExited += (s, _) =>
        {
            if (s is Microsoft.UI.Xaml.Controls.Border b) b.Background = theme.ActionButtonBackground;
        };
        btn.PointerPressed += (_, _) => command.Execute(null);

        return btn;
    }
}
