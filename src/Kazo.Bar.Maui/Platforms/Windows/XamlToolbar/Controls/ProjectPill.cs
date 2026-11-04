// All types explicitly qualified to avoid MAUI vs WinUI3 ambiguity
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.ViewModels;
using WinSolid = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.Controls;

/// <summary>
/// The project selector pill (Zone 1). Displays GitHub icon + project name.
/// Clicking opens a Flyout with the project list.
/// </summary>
public class ProjectPill : UserControl
{
    private readonly TextBlock _projectNameText;
    private readonly Flyout _flyout;
    private readonly StackPanel _flyoutContent;

    public ProjectPill()
    {
        _flyoutContent = new StackPanel { Spacing = 2, MinWidth = 200 };

        _flyout = new Flyout
        {
            Content = _flyoutContent,
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft,
            ShouldConstrainToRootBounds = false,
        };

        var githubIcon = new FontIcon
        {
            Glyph = "\uE895",
            FontSize = 18,
            Foreground = new WinSolid(global::Windows.UI.Color.FromArgb(255, 255, 255, 255)),
        };

        _projectNameText = new TextBlock
        {
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new WinSolid(global::Windows.UI.Color.FromArgb(255, 255, 255, 255)),
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 160,
        };

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
        };
        contentPanel.Children.Add(githubIcon);
        contentPanel.Children.Add(_projectNameText);

        var pill = new Microsoft.UI.Xaml.Controls.Border
        {
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(28),
            Height = 44,
            Padding = new Microsoft.UI.Xaml.Thickness(16, 0, 24, 0),
            Child = contentPanel,
        };

        pill.PointerPressed += OnPillPressed;
        Content = pill;
        Canvas.SetZIndex(this, 20);
    }

    public void ApplyViewModel(ToolbarViewModel vm)
    {
        _projectNameText.SetBinding(TextBlock.TextProperty,
            new Microsoft.UI.Xaml.Data.Binding { Source = vm, Path = new PropertyPath("ProjectName"), Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay });

        if (Content is Microsoft.UI.Xaml.Controls.Border pill)
        {
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.Theme))
                    pill.Background = vm.Theme.ProjectPillBackground;
            };
            pill.Background = vm.Theme.ProjectPillBackground;
        }

        RebuildFlyoutItems(vm);
        vm.Projects.CollectionChanged += (_, _) => RebuildFlyoutItems(vm);
    }

    private void RebuildFlyoutItems(ToolbarViewModel vm)
    {
        _flyoutContent.Children.Clear();
        foreach (var project in vm.Projects)
        {
            var icon = new FontIcon
            {
                Glyph = "\uE895",
                FontSize = 16,
                Foreground = vm.Theme.DropdownText,
            };

            var text = new TextBlock
            {
                Text = project.Name,
                FontSize = 14,
                Foreground = vm.Theme.DropdownText,
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Padding = new Microsoft.UI.Xaml.Thickness(12, 10, 12, 10),
            };
            row.Children.Add(icon);
            row.Children.Add(text);

            row.PointerEntered += (s, _) =>
            {
                if (s is StackPanel sp) sp.Background = vm.Theme.DropdownItemHover;
            };
            row.PointerExited += (s, _) =>
            {
                if (s is StackPanel sp) sp.Background = null;
            };
            row.PointerPressed += (_, _) =>
            {
                vm.SelectProjectCommand.Execute(project);
                _flyout.Hide();
            };

            _flyoutContent.Children.Add(row);
        }
    }

    private void OnPillPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            _flyout.ShowAt(fe);
    }
}
