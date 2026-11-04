namespace Kazo.Bar.Maui.Controls;

/// <summary>
/// Zone 3: Action buttons area. Three circular glass-style buttons.
/// Handles pointer hover effects for visual feedback.
/// </summary>
public partial class ActionBarView : ContentView
{
    public ActionBarView()
    {
        InitializeComponent();
    }

    private void OnButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border &&
            Application.Current?.Resources.TryGetValue("ActionButtonHoverBackground", out var brush) == true &&
            brush is Brush hoverBrush)
        {
            border.Background = hoverBrush;
        }
    }

    private void OnButtonPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border &&
            Application.Current?.Resources.TryGetValue("ActionButtonBackground", out var brush) == true &&
            brush is Brush normalBrush)
        {
            border.Background = normalBrush;
        }
    }
}
