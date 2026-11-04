using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.ViewModels;
using WinSolid = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.Controls;

/// <summary>
/// Git branch pill (Zone 2). Overlaps under the Project pill with negative margin.
/// Displays: git-branch icon + branch name + sync arrows.
/// </summary>
public class GitBranchPill : UserControl
{
    private readonly TextBlock _branchNameText;
    private readonly Microsoft.UI.Xaml.Controls.Border _pill;

    public GitBranchPill()
    {
        var branchIcon = new FontIcon
        {
            Glyph = "\uE8D4",
            FontSize = 16,
            Foreground = new WinSolid(global::Windows.UI.Color.FromArgb(230, 255, 255, 255)),
        };

        _branchNameText = new TextBlock
        {
            FontSize = 14,
            Foreground = new WinSolid(global::Windows.UI.Color.FromArgb(230, 255, 255, 255)),
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 120,
        };

        var syncIcon = new FontIcon
        {
            Glyph = "\uE8AB",
            FontSize = 12,
            Foreground = new WinSolid(global::Windows.UI.Color.FromArgb(180, 255, 255, 255)),
        };

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
        };
        contentPanel.Children.Add(branchIcon);
        contentPanel.Children.Add(_branchNameText);
        contentPanel.Children.Add(syncIcon);

        _pill = new Microsoft.UI.Xaml.Controls.Border
        {
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0, 28, 28, 0),
            Height = 44,
            Padding = new Microsoft.UI.Xaml.Thickness(28, 0, 16, 0),
            Child = contentPanel,
        };

        Margin = new Microsoft.UI.Xaml.Thickness(-18, 0, 0, 0);
        Canvas.SetZIndex(this, 10);
        Content = _pill;
    }

    public void ApplyViewModel(ToolbarViewModel vm)
    {
        _branchNameText.SetBinding(TextBlock.TextProperty,
            new Microsoft.UI.Xaml.Data.Binding { Source = vm, Path = new PropertyPath("BranchName"), Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay });

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Theme))
                _pill.Background = vm.Theme.GitPillBackground;
        };
        _pill.Background = vm.Theme.GitPillBackground;
    }
}
