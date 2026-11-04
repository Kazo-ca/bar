namespace Kazo.Bar.Maui.Controls;

/// <summary>
/// Zone 4: System pill. Timer display + settings gear button.
/// Handles hover effect on the settings button.
/// </summary>
public partial class SystemPillView : ContentView
{
    public SystemPillView()
    {
        InitializeComponent();
    }

    private void OnSettingsPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.BackgroundColor = Color.FromArgb("#1EFFFFFF");
    }

    private void OnSettingsPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.BackgroundColor = Colors.Transparent;
    }
}
